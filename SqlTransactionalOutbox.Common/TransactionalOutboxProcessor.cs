using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.Utilities;

namespace SqlTransactionalOutbox
{
    /// <summary>
    /// Process all pending items in the transactional outbox using the specified ISqlTransactionalOutboxRepository,
    /// ISqlTransactionalOutboxPublisher, & OutboxProcessingOptions
    /// </summary>
    public class TransactionalOutboxProcessor<TUniqueIdentifier, TPayload> : ISqlTransactionalOutboxProcessor<TUniqueIdentifier>
    {
        public ISqlTransactionalOutboxRepository<TUniqueIdentifier, TPayload> OutboxRepository { get; }
        public ISqlTransactionalOutboxPublisher<TUniqueIdentifier> OutboxPublisher { get; }

        public TransactionalOutboxProcessor(
            ISqlTransactionalOutboxRepository<TUniqueIdentifier, TPayload> outboxRepository,
            ISqlTransactionalOutboxPublisher<TUniqueIdentifier> outboxPublisher
        )
        {
            this.OutboxRepository = outboxRepository ?? throw new ArgumentNullException(nameof(OutboxRepository));
            this.OutboxPublisher = outboxPublisher ?? throw new ArgumentNullException(nameof(OutboxPublisher));
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
            var pendingOutboxItems = await OutboxRepository.RetrieveOutboxItemsAsync(
                OutboxItemStatus.Pending,
                options.ItemProcessingBatchSize
            ).ConfigureAwait(false);

            results.ProcessingTimer.Stop();

            //Short Circuit if there is nothing to process!
            if (pendingOutboxItems.Count <= 0)
            {
                options.LogDebugCallback?.Invoke($"There are no outbox items to process; processing completed at [{DateTime.Now}].");
                return results;
            }

            options.LogDebugCallback?.Invoke(
                $"Starting Outbox Processing of [{pendingOutboxItems.Count}] outbox items, retrieved in" +
                    $" [{results.ProcessingTimer.Elapsed.ToElapsedTimeDescriptiveFormat()}], at [{DateTime.Now}]..."
            );

            //Attempt the acquire the Distributed Mutex Lock (if specified)!
            options.LogDebugCallback?.Invoke($"{nameof(options.FifoEnforcedPublishingEnabled)} = {options.FifoEnforcedPublishingEnabled}");

            results.ProcessingTimer.Start();

            await using var distributedMutex = options.FifoEnforcedPublishingEnabled
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
                pendingOutboxItems,
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

            var skipFifoGroups = new HashSet<string>();

            foreach(var item in outboxItems)
            {
                //Process the item when Fifo Publishing is Disabled, or the Item has no Fifo Group specified, or the
                //  specified Fifo Group Id is not already identified as one that needs to be skipped due to an item error
                //  that belongs to that group!
                if (!options.FifoEnforcedPublishingEnabled  // Fifo is Disabled
                    || string.IsNullOrWhiteSpace(item.FifoGroupingIdentifier) // No Fifo Group is defined
                    || !skipFifoGroups.Contains(item.FifoGroupingIdentifier) // Fifo group defined is not in the set to be skipped
                ) 
                {
                    try
                    {
                        await ProcessSingleOutboxItemInternal(item, options, results);
                    }
                    catch (Exception itemException)
                    {
                        await HandleExceptionForOutboxItemFromQueueInternal(
                            item, itemException, options, results, throwExceptionOnFailure, skipFifoGroups
                        );
                    }
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

        protected virtual async Task ProcessSingleOutboxItemInternal(
            ISqlTransactionalOutboxItem<TUniqueIdentifier> item,
            OutboxProcessingOptions options,
            OutboxProcessingResults<TUniqueIdentifier> results
        )
        {
            //This process will publish pending items, while also cleaning up Pending Items that need to be Failed because
            //  they couldn't be successfully published before and are exceeding the currently configured limits for
            //  retry attempts and/or time-to-live.
            //NOTE: IF any of this fails due to issues with Sql Server it's ok because we have already transaction-ally
            //      secured the data in the outbox therefore we can repeat processing over-and-over under the promise of
            //      'at-least-once' publishing attempt.
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
            else if (options.TimeSpanToLive > TimeSpan.Zero && DateTimeOffset.UtcNow.Subtract(item.CreatedDateTimeUtc) >= options.TimeSpanToLive)
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
                await OutboxPublisher.PublishOutboxItemAsync(item, options.FifoEnforcedPublishingEnabled).ConfigureAwait(false);

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
        
        protected async Task HandleExceptionForOutboxItemFromQueueInternal(
            ISqlTransactionalOutboxItem<TUniqueIdentifier> item,
            Exception itemException,
            OutboxProcessingOptions options,
            OutboxProcessingResults<TUniqueIdentifier> results,
            bool throwExceptionOnFailure,
            HashSet<string> skipFifoGroups
        )
        {
            var errorMessage = $"An Unexpected Exception occurred while processing outbox item [{item.UniqueIdentifier}].";

            var fifoErrorMessage = $"FIFO Processing is enabled, but the outbox item [{item.UniqueIdentifier}] could not be published;" +
                                            $" all associated items for the Fifo Group [{item.FifoGroupingIdentifier}] will be skipped.";

            if (options.FifoEnforcedPublishingEnabled)
                errorMessage = string.Concat(errorMessage, fifoErrorMessage);

            var processingException = new Exception(errorMessage, itemException);

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
            else if (options.FifoEnforcedPublishingEnabled)
            {
                //If Enforce Fifo processing is enabled then we also need to halt processing and drain the current queue to preserve 
                //  the current order of processing; if we allow it to proceed we might publish the next item out of order.
                //NOTE: This may create a block in the Queue if the issue isn't fixed but it's necessary to preserve
                //      the publishing order until the erroneous blocking item is resolved automatically (via connections restored,
                //      or item fails due to re-attempts), or manually (item is failed and/or removed manually).
                options.LogDebugCallback?.Invoke(
                    $"FIFO Processing is enabled, but the outbox item [{item.UniqueIdentifier}] could not be published;" +
                        $" all following items for the FIFO Group [{item.FifoGroupingIdentifier}] will be skipped."
                );

                skipFifoGroups.Add(item.FifoGroupingIdentifier);

                //ORIGINAL Logic before Fifo Grouping Identifiers were implemented...
                //processingException = new Exception(
                //    $"The processing must be stopped because [{nameof(options.FifoEnforcedPublishingEnabled)}]"
                //    + $" configuration option is enabled; therefore to preserve the publishing order the remaining"
                //    + $" [{processingQueue.Count}] items will be delayed until the current item [{item.UniqueIdentifier}]"
                //    + " can be processed successfully or is failed out of the outbox queue.",
                //    processingException
                //);

                ////Drain the Queue to halt current process...
                //while (processingQueue.Count > 0)
                //{
                //    results.SkippedItems.Add(processingQueue.Dequeue());
                //}
            }

            //Log the latest initialized exception with details...
            options.LogErrorCallback?.Invoke(processingException);
        }
    }
}
