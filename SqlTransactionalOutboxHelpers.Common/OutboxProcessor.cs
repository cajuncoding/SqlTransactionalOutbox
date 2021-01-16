using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SqlTransactionalOutboxHelpers.CustomExtensions;

namespace SqlTransactionalOutboxHelpers
{
    /// <summary>
    /// Process all pending items in the transactional outbox using the specified ISqlTransactionalOutboxRepository,
    /// ISqlTransactionalOutboxPublisher, & OutboxProcessingOptions
    /// </summary>
    public class OutboxProcessor<TPayload> : ISqlTransactionalOutboxProcessor<TPayload>
    {
        protected ISqlTransactionalOutboxRepository OutboxRepository { get; }
        protected ISqlTransactionalOutboxPublisher OutboxPublisher { get; }
        protected ISqlTransactionalOutboxSerializer PayloadSerializer { get; }
        protected ISqlTransactionalOutboxItemFactory OutboxItemFactory { get; }

        public OutboxProcessor(
            ISqlTransactionalOutboxRepository outboxRepository,
            ISqlTransactionalOutboxPublisher outboxPublisher,
            ISqlTransactionalOutboxSerializer payloadSerializer = null,
            ISqlTransactionalOutboxItemFactory outboxItemFactory = null
        )
        {
            this.OutboxRepository = outboxRepository ?? throw new ArgumentNullException(nameof(OutboxRepository));
            this.OutboxPublisher = outboxPublisher ?? throw new ArgumentNullException(nameof(OutboxPublisher));
            this.PayloadSerializer = payloadSerializer ?? new JsonOutboxPayloadSerializer();
        }

        public virtual async Task ProcessCleanupOfOutboxHistoricalItemsAsync(TimeSpan historyTimeToKeepTimeSpan)
        {
            //Cleanup the Historical data using the Repository...
            await OutboxRepository.CleanupOutboxHistoricalItemsAsync(historyTimeToKeepTimeSpan).ConfigureAwait(false);
        }
        
        public async Task<ISqlTransactionalOutboxItem> InsertNewPendingOutboxItemAsync(string publishingTarget, TPayload publishingPayload)
        {
            //Use the Outbox Item Factory to create a new Outbox Item (serialization of the Payload will be handled by the Factory).
            var outboxInsertItem = new OutboxInsertItem<TPayload>(publishingTarget, publishingPayload);

            //Store the outbox item using the Repository...
            var resultItems = await InsertNewPendingOutboxItemsAsync(
                new List<OutboxInsertItem<TPayload>>() { outboxInsertItem }
            ).ConfigureAwait(false);

            return resultItems.FirstOrDefault();
        }

        public async Task<IEnumerable<ISqlTransactionalOutboxItem>> InsertNewPendingOutboxItemsAsync(IEnumerable<OutboxInsertItem<TPayload>> outboxInsertItems)
        {
            //Use the Outbox Item Factory to create a new Outbox Item with serialized payload.
            var outboxItems = outboxInsertItems.Select(i =>
            {
                var serializedPayload = PayloadSerializer.SerializePayload(i.PublishingPayload);
                return OutboxItemFactory.CreateNewOutboxItem(i.PublishingTarget, serializedPayload);
            });

            //Store the outbox item using the Repository...
            var resultItems = await OutboxRepository.InsertNewOutboxItemsAsync(outboxItems).ConfigureAwait(false);

            return resultItems;
        }

        public virtual async Task<OutboxProcessingResults> ProcessPendingOutboxItemsAsync(
            OutboxProcessingOptions processingOptions = null,
            bool throwExceptionOnFailure = false
        )
        {
            var options = processingOptions ?? OutboxProcessingOptions.DefaultOutboxProcessingOptions;
            var results = new OutboxProcessingResults();

            results.ProcessingTimer.Start();

            //Retrieve items to e processed from the Repository (ALL Pending items available for publishing attempt!)
            var outboxItems = await OutboxRepository.RetrieveOutboxItemsAsync(
                OutboxItemStatus.Pending, 
                options.ItemProcessingBatchSize
            ).ConfigureAwait(false);

            results.ProcessingTimer.Stop();

            //Short Circuit if there is nothing to process!
            if (outboxItems.Count <= 0)
            {
                options.LogDebugCallback?.Invoke(
                    $"There are no outbox items to process; processing completed at [{DateTime.Now}]."
                );
                return results;
            }

            options.LogDebugCallback?.Invoke(
                $"Starting Outbox Processing of [{outboxItems.Count}] outbox items, retrieved in" +
                    $" [{results.ProcessingTimer.Elapsed.ToElapsedTimeDescriptiveFormat()}], at [{DateTime.Now}]..."
            );

            //Attempt the acquire the Distributed Mutex Lock (if specified)!
            options.LogDebugCallback?.Invoke($"{nameof(options.EnableDistributedMutexLockForFifoPublishingOrder)} = {options.EnableDistributedMutexLockForFifoPublishingOrder}");

            results.ProcessingTimer.Start();

            await using var distributedMutex = options.EnableDistributedMutexLockForFifoPublishingOrder
                ? await OutboxRepository.AcquireDistributedProcessingMutexAsync().ConfigureAwait(false)
                : new NoOpAsyncDisposable();

            //The distributed Mutex will ONLY be null if it could not be acquired; otherwise our
            //  NoOp will be successfully initialized if locking is disabled.
            if (distributedMutex == null)
            {
                var mutexErrorMessage = "Distributed Mutex Lock could not be acquired; processing will not continue.";
                options.LogDebugCallback?.Invoke(mutexErrorMessage);

                if (throwExceptionOnFailure) 
                    throw new Exception(mutexErrorMessage);
                
                return results;
            }

            //Now process the Items and Update the Outbox appropriately...
            var processedItems = await ProcessOutboxItemsInternal(
                outboxItems, 
                options, 
                results, 
                throwExceptionOnFailure
            ).ConfigureAwait(false);

            //Update & store the state for all Items Processed (e.g. Successful, Attempted, Failed, etc.)!
            options.LogDebugCallback?.Invoke($"Starting to update the Outbox for [{processedItems.Count}] items...");
            
            results.ProcessingTimer.Start();
            await OutboxRepository.UpdateOutboxItemsAsync(processedItems, options.ItemUpdatingBatchSize).ConfigureAwait(false);
            results.ProcessingTimer.Stop();

            options.LogDebugCallback?.Invoke(
                $"Finished updating the Outbox for [{processedItems.Count}] items in" +
                    $" [{results.ProcessingTimer.Elapsed.ToElapsedTimeDescriptiveFormat()}]!"
            );

            return results;
        }

        public async Task<List<ISqlTransactionalOutboxItem>> ProcessOutboxItemsInternal(
            List<ISqlTransactionalOutboxItem> outboxItems, 
            OutboxProcessingOptions options,
            OutboxProcessingResults results,
            bool throwExceptionOnFailure
        )
        {
            var processedItems = new List<ISqlTransactionalOutboxItem>();

            //We always sort data to generally maintain FIFO processing order, but parallelism risk is only mitigated
            //  by the above Mutex locking...
            outboxItems = outboxItems.OrderBy(i => i.CreatedDateTimeUtc).ToList();

            //This process will publish pending items, while also cleaning up Pending Items that need to be Failed because
            //  they couldn't be successfully published before and are exceeding the currently configured limits for
            //  retry attempts and/or time-to-live.
            //NOTE: IF any of this fails due to issues with Sql Server it's ok because we have alreayd transactionally
            //      secured the data in the outbox therefore we can repeat processing over-and-over under the promise of
            //      'at-least-once' publishing attempt.
            foreach (var item in outboxItems)
            {
                try
                {
                    options.LogDebugCallback?.Invoke($"Processing Item [{item.UniqueIdentifier}]...");

                    //Validate the Item hasn't exceeded the Max Retry Attempts if enabled in the options...
                    if (options.MaxPublishingAttempts > 0 && item.PublishingAttempts >= options.MaxPublishingAttempts)
                    {
                        options.LogDebugCallback?.Invoke(
                            $"Item [{item.UniqueIdentifier}] has failed due to exceeding the max number of" +
                                $" publishing attempts [{options.MaxPublishingAttempts}] with current PublishingAttempts=[{item.PublishingAttempts}]."
                        );

                        item.Status = OutboxItemStatus.FailedAttemptsExceeded;
                        results.FailedItems.Add(item);
                    }
                    //Validate the Item hasn't expired if enabled in the options...
                    else if (options.TimeSpanToLive > TimeSpan.Zero && item.CreatedDateTimeUtc.Add(options.TimeSpanToLive) >= DateTime.UtcNow)
                    {
                        options.LogDebugCallback?.Invoke(
                            $"Item [{item.UniqueIdentifier}] has failed due to exceeding the maximum time-to-live" +
                                $" [{options.TimeSpanToLive.ToElapsedTimeDescriptiveFormat()}] because it was created at [{item.CreatedDateTimeUtc}] UTC."
                        );

                        item.Status = OutboxItemStatus.FailedExpired;
                        results.FailedItems.Add(item);
                    }
                    //Finally attempt to publish the item...
                    else
                    {
                        item.PublishingAttempts++;
                        await OutboxPublisher.PublishOutboxItemAsync(item).ConfigureAwait(false);

                        options.LogDebugCallback?.Invoke(
                            $"Item [{item.UniqueIdentifier}] published successfully after [{item.PublishingAttempts}] publishing attempts!"
                        );

                        //Update the Status only AFTER successful Publishing!
                        item.Status = OutboxItemStatus.Successful;
                        processedItems.Add(item);
                        results.SuccessfullyPublishedItems.Add(item);

                        options.LogDebugCallback?.Invoke(
                            $"Item [{item.UniqueIdentifier}] outbox status will be updated to [{item.Status}]."
                        );
                    }
                }
                catch (Exception exc)
                {
                    var processingException = new Exception(
                        message: $"An Exception occurred while processing outbox item [{item.UniqueIdentifier}].",
                        innerException: exc
                    );

                    options.LogErrorCallback?.Invoke(processingException);

                    //Short circuit if we are configured to Throw the Error!
                    if (throwExceptionOnFailure)
                    {
                        await OutboxRepository.UpdateOutboxItemsAsync(processedItems).ConfigureAwait(false);
                        throw;
                    }

                    //Add Failed Item to the results
                    results.FailedItems.Add(item);
                }
            }

            results.ProcessingTimer.Stop();
            options.LogDebugCallback?.Invoke(
                $"Finished Publishing [{processedItems.Count}] items in" +
                    $" [{results.ProcessingTimer.Elapsed.ToElapsedTimeDescriptiveFormat()}]."
            );

            return processedItems;
        }
    }
}
