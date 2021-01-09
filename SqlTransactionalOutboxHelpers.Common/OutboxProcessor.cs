using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SqlTransactionalOutboxHelpers
{
    public interface ISqlTransactionalOutboxProcessor
    {
        Task<OutboxProcessingResults> ProcessPendingOutboxItemsAsync(
            OutboxProcessingOptions processingOptions = null,
            bool throwExceptionOnFailure = false
        );
    }

    /// <summary>
    /// Process all pending items in the transactional outbox using the specified ISqlTransactionalOutboxRepository,
    /// ISqlTransactionalOutboxPublisher, & OutboxProcessingOptions
    /// </summary>
    public class OutboxProcessor : ISqlTransactionalOutboxProcessor
    {
        public static OutboxProcessingOptions DefaultOutboxProcessingOptions = new OutboxProcessingOptions();

        protected ISqlTransactionalOutboxRepository OutboxRepository { get; }
        protected ISqlTransactionalOutboxPublisher OutboxPublisher { get; }

        public OutboxProcessor(
            ISqlTransactionalOutboxRepository outboxRepository,
            ISqlTransactionalOutboxPublisher outboxPublisher)
        {
            this.OutboxRepository = outboxRepository ?? throw new ArgumentNullException(nameof(OutboxRepository));
            this.OutboxPublisher = outboxPublisher ?? throw new ArgumentNullException(nameof(OutboxPublisher));
        }

        public virtual async Task<OutboxProcessingResults> ProcessPendingOutboxItemsAsync(
            OutboxProcessingOptions processingOptions = null,
            bool throwExceptionOnFailure = false
        )
        {
            var options = processingOptions ?? DefaultOutboxProcessingOptions;
            var results = new OutboxProcessingResults();
            var processedItems = new List<OutboxItem>();

            //Retrieve items to e processed from the Repository (ALL Pending items available for publishing attempt!)
            var outboxItems = await OutboxRepository.RetrievePendingOutboxItemsAsync();
            
            //Short Circuit if there is nothing to process!
            if (outboxItems.Count <= 0) 
                return results;

            results.ProcessingTimer.Start();

            await using var distributedMutex = options.EnableDistributedMutexLockForFIFOPublishingOrder
                ? await OutboxRepository.AcquireDistributedProcessingMutexAsync()
                : new NoOpAsyncDisposable();

            foreach (var item in outboxItems)
            {
                try
                {
                    //Validate the Item hasn't exceeded the Max Retry Attempts if enabled in the options...
                    if (options.MaxPublishingAttempts > 0 && item.PublishingAttempts >= options.MaxPublishingAttempts)
                    {
                        item.Status = OutboxItemStatus.FailedAttemptsExceeded;
                        results.FailedItems.Add(item);
                    }
                    //Validate the Item hasn't expired if enabled in the options...
                    else if (options.ExpirationTimeSpan > TimeSpan.Zero && item.CreatedDateTimeUtc.Add(options.ExpirationTimeSpan) >= DateTime.UtcNow)
                    {
                        item.Status = OutboxItemStatus.FailedExpired;
                        results.FailedItems.Add(item);
                    }
                    else
                    {
                        //Finally attempt to publish the item...
                        item.PublishingAttempts++;

                        //Update the Status only AFTER successful Publishing!
                        item.Status = OutboxItemStatus.Successful;
                        processedItems.Add(item);
                        results.SuccessfullyPublishedItems.Add(item);
                    }
                }
                catch (Exception)
                {
                    //Short circuit if we are configured to Throw the Error!
                    if (throwExceptionOnFailure)
                    {
                        await OutboxRepository.UpdateOutboxItemsAsync(processedItems);
                        throw;
                    }
                         
                    //Add Failed Item to the results
                    results.FailedItems.Add(item);
                }
            }

            //Update & store the state for all Items Processed (e.g. Successful, Attempted, Failed, etc.)!
            await OutboxRepository.UpdateOutboxItemsAsync(processedItems);

            results.ProcessingTimer.Stop();
            return results;
        }


    }
}
