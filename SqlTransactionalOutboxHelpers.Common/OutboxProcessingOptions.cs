using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutboxHelpers
{
    public class OutboxProcessingOptions
    {
        /// <summary>
        /// Defines the maximum batch size (number) of items that can be processed per execution;
        /// value of -1 disables batching and allow all items.
        /// </summary>
        public int MaxItemBatchSize { get; set; } = -1;

        /// <summary>
        /// The Maximum number of publishing retry attempts that can be made before the item fails;
        /// value of -1 disables this and allows an infinite number of retry attempts.
        /// </summary>
        public int MaxPublishingAttempts { get; set; } = -1;

        /// <summary>
        /// The Maximum amount of time the item may exist before it is set as Expired;
        /// the Zero Timespan disables this and allows items to be retried for an infinite amount of time.
        /// </summary>
        public TimeSpan ExpirationTimeSpan { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Determine if we should enforce FIFO Publishing order which requires the use of
        /// a distributed application mutex lock to ensure that only one processor can execute
        /// at any time -- eliminating risk of parallel and potential impacts to processing order.
        /// </summary>
        public bool EnableDistributedMutexLockForFIFOPublishingOrder { get; set; } = false;
    }
}
