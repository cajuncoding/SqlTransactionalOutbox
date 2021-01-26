using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox
{
    /// <summary>
    /// Process all pending items in the transactional outbox using the specified ISqlTransactionalOutboxRepository,
    /// ISqlTransactionalOutboxPublisher, & OutboxProcessingOptions
    /// </summary>
    public class OutboxProcessor<TUniqueIdentifier, TPayload> : ISqlTransactionalOutboxProcessor<TUniqueIdentifier, TPayload>
    {
        public ISqlTransactionalOutboxRepository<TUniqueIdentifier, TPayload> OutboxRepository { get; }
        public ISqlTransactionalOutboxPublisher<TUniqueIdentifier> OutboxPublisher { get; }

        public OutboxProcessor(
            ISqlTransactionalOutboxRepository<TUniqueIdentifier, TPayload> outboxRepository,
            ISqlTransactionalOutboxPublisher<TUniqueIdentifier> outboxPublisher
        )
        {
            this.OutboxRepository = outboxRepository ?? throw new ArgumentNullException(nameof(OutboxRepository));
            this.OutboxPublisher = outboxPublisher ?? throw new ArgumentNullException(nameof(OutboxPublisher));
        }

        public virtual async Task ProcessCleanupOfOutboxHistoricalItemsAsync(TimeSpan historyTimeToKeepTimeSpan)
        {
            //Cleanup the Historical data using the Repository...
            await OutboxRepository.CleanupOutboxHistoricalItemsAsync(historyTimeToKeepTimeSpan).ConfigureAwait(false);
        }
        
        public virtual async Task<ISqlTransactionalOutboxItem<TUniqueIdentifier>> InsertNewPendingOutboxItemAsync(
            string publishingTarget, 
            TPayload publishingPayload
        )
        {
            //Use the Outbox Item Factory to create a new Outbox Item (serialization of the Payload will be handled by the Factory).
            var outboxInsertItem = new OutboxInsertionItem<TPayload>(publishingTarget, publishingPayload);

            //Store the outbox item using the Repository...
            var resultItems = await InsertNewPendingOutboxItemsAsync(
                new List<ISqlTransactionalOutboxInsertionItem<TPayload>>() { outboxInsertItem }
            ).ConfigureAwait(false);

            return resultItems.FirstOrDefault();
        }

        public virtual async Task<List<ISqlTransactionalOutboxItem<TUniqueIdentifier>>> InsertNewPendingOutboxItemsAsync(
            IEnumerable<ISqlTransactionalOutboxInsertionItem<TPayload>> outboxInsertionItems
        )
        {
            //Store the outbox item using the Repository...
            var resultItems = await OutboxRepository.InsertNewOutboxItemsAsync(outboxInsertionItems).ConfigureAwait(false);

            return resultItems;
        }

        public virtual async Task<ISqlTransactionalOutboxProcessingResults<TUniqueIdentifier>> ProcessPendingOutboxItemsAsync(
            OutboxProcessingOptions processingOptions = null,
            bool throwExceptionOnFailure = false
        )
        {
            var options = processingOptions ?? OutboxProcessingOptions.DefaultOutboxProcessingOptions;
            var results = new OutboxProcessingResults<TUniqueIdentifier>();

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
                const string mutexErrorMessage = "Distributed Mutex Lock could not be acquired; processing will not continue.";
                options.LogDebugCallback?.Invoke(mutexErrorMessage);

                if (throwExceptionOnFailure) 
                    throw new Exception(mutexErrorMessage);
                
                return results;
            }

            //Now EXECUTE & PROCESS the Items and Update the Outbox appropriately...
            //NOTE: It's CRITICAL that we attempt to Publish BEFORE we update the results in the TransactionalOutbox,
            //      this ensures that we guarantee at-least-once delivery because the item will be retried at a later point
            //      if anything fails with the update.
            await ProcessOutboxItemsInternalAsync(
                outboxItems, 
                options, 
                results, 
                throwExceptionOnFailure
            ).ConfigureAwait(false);

            return results;
        }

        protected virtual async Task ProcessOutboxItemsInternalAsync(
            List<ISqlTransactionalOutboxItem<TUniqueIdentifier>> outboxItems, 
            OutboxProcessingOptions options,
            OutboxProcessingResults<TUniqueIdentifier> results,
            bool throwExceptionOnFailure
        )
        {
            //Convert the list to a Queue for easier processing...
            var processingQueue = new Queue<ISqlTransactionalOutboxItem<TUniqueIdentifier>>(outboxItems);

            //This process will publish pending items, while also cleaning up Pending Items that need to be Failed because
            //  they couldn't be successfully published before and are exceeding the currently configured limits for
            //  retry attempts and/or time-to-live.
            //NOTE: IF any of this fails due to issues with Sql Server it's ok because we have already transaction-ally
            //      secured the data in the outbox therefore we can repeat processing over-and-over under the promise of
            //      'at-least-once' publishing attempt.
            while (processingQueue.Count > 0)
            {
                var item = processingQueue.Dequeue();

                try
                {
                    await ProcessOutboxItemFromQueueInternal(item, options, results);
                }
                catch (Exception exc)
                {
                    await HandleExceptionForOutboxItemFromQueueInternal(processingQueue, item, exc, options, results, throwExceptionOnFailure);
                }
            }

            results.ProcessingTimer.Stop();
            options.LogDebugCallback?.Invoke(
                $"Finished Publishing [{results.SuccessfullyPublishedItems.Count}] items in" +
                    $" [{results.ProcessingTimer.Elapsed.ToElapsedTimeDescriptiveFormat()}]."
            );
            
            //Store all updated results back into the Outbox!
            await UpdateProcessedItemsInternal(results, options);
            
        }

        protected virtual async Task UpdateProcessedItemsInternal(
            OutboxProcessingResults<TUniqueIdentifier> results, 
            OutboxProcessingOptions options
        )
        {
            //Update & store the state for all Items Processed (e.g. Successful, Attempted, Failed, etc.)!
            var processedItems = results.GetAllProcessedItems();

            options.LogDebugCallback?.Invoke($"Starting to update the Outbox for [{processedItems.Count}] items...");

            if (processedItems.Count > 0)
            {
                results.ProcessingTimer.Start();
                await OutboxRepository.UpdateOutboxItemsAsync(processedItems, options.ItemUpdatingBatchSize).ConfigureAwait(false);
                results.ProcessingTimer.Stop();
            }

            options.LogDebugCallback?.Invoke(
                $"Finished updating the Outbox for [{processedItems.Count}] items in" +
                $" [{results.ProcessingTimer.Elapsed.ToElapsedTimeDescriptiveFormat()}]!"
            );
        }

        protected virtual async Task ProcessOutboxItemFromQueueInternal(
            ISqlTransactionalOutboxItem<TUniqueIdentifier> item,
            OutboxProcessingOptions options,
            OutboxProcessingResults<TUniqueIdentifier> results
        )
        {
            options.LogDebugCallback?.Invoke($"Processing Item [{item.UniqueIdentifier}]...");

            //Validate the Item hasn't exceeded the Max Retry Attempts if enabled in the options...
            if (options.MaxPublishingAttempts > 0 && item.PublishAttempts >= options.MaxPublishingAttempts)
            {
                options.LogDebugCallback?.Invoke(
                    $"Item [{item.UniqueIdentifier}] has failed due to exceeding the max number of" +
                        $" publishing attempts [{options.MaxPublishingAttempts}] with current PublishAttempts=[{item.PublishAttempts}]."
                );

                item.Status = OutboxItemStatus.FailedAttemptsExceeded;
                results.FailedItems.Add(item);
            }
            //Validate the Item hasn't expired if enabled in the options...
            else if (options.TimeSpanToLive > TimeSpan.Zero && DateTime.UtcNow.Subtract(item.CreatedDateTimeUtc) >= options.TimeSpanToLive)
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
                item.PublishAttempts++;
                await OutboxPublisher.PublishOutboxItemAsync(item).ConfigureAwait(false);

                options.LogDebugCallback?.Invoke(
                    $"Item [{item.UniqueIdentifier}] published successfully after [{item.PublishAttempts}] publishing attempt(s)!"
                );

                //Update the Status only AFTER successful Publishing!
                item.Status = OutboxItemStatus.Successful;
                results.SuccessfullyPublishedItems.Add(item);

                options.LogDebugCallback?.Invoke(
                    $"Item [{item.UniqueIdentifier}] outbox status will be updated to [{item.Status}]."
                );
            }
        }

        public async Task HandleExceptionForOutboxItemFromQueueInternal(
            Queue<ISqlTransactionalOutboxItem<TUniqueIdentifier>> processingQueue,
            ISqlTransactionalOutboxItem<TUniqueIdentifier> item,
            Exception itemException,
            OutboxProcessingOptions options,
            OutboxProcessingResults<TUniqueIdentifier> results,
            bool throwExceptionOnFailure
        )
        {
            var processingException = new Exception(
                message: $"An Unexpected Exception occurred while processing outbox item [{item.UniqueIdentifier}].",
                innerException: itemException
            );

            //Add Failed Item to the results
            results.FailedItems.Add(item);

            //Short circuit if we are configured to Throw the Error or if Enforce FIFO processing is enabled!
            if (throwExceptionOnFailure)
            {
                options.LogErrorCallback?.Invoke(processingException);

                //If configured to throw an error then we Attempt to update the item before throwing exception
                // because normally it would have been updated in bulk if exceptions were suppressed.
                //NOTE: NO need to process results since we are throwing an Exception...
                await UpdateProcessedItemsInternal(results, options);

                throw processingException;
            }

            //If Enforce Fifo processing is enabled then we also need to halt processing and drain the current queue to preserve 
            //  the current order of processing; if we allow it to proceed we might publish the next item out of order.
            //NOTE: This may create a block in the Queue if the issue isn't fixed but it's necessary to preserve
            //      the publishing order until the erroneous blocking item is resolved automatically (via connections restored,
            //      or item fails due to re-attempts), or manually (item is failed and/or removed manually).
            if (options.EnableDistributedMutexLockForFifoPublishingOrder)
            {
                processingException = new Exception(
                    $"The processing must be stopped because [{nameof(options.EnableDistributedMutexLockForFifoPublishingOrder)}]"
                    + $" configuration option is enabled; therefore to preserve the publishing order the remaining"
                    + $" [{processingQueue.Count}] items will be delayed until the current item [{item.UniqueIdentifier}]"
                    + " can be processed successfully or is failed out of the outbox queue.",
                    processingException
                );

                //Drain the Queue to halt current process...
                while (processingQueue.Count > 0)
                {
                    results.SkippedItems.Add(processingQueue.Dequeue());
                }
            }

            //Log the latest initialized exception with details...
            options.LogErrorCallback?.Invoke(processingException);
        }
    }
}
