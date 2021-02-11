using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutbox
{
    public class OutboxProcessingOptions
    {
        public static OutboxProcessingOptions DefaultOutboxProcessingOptions = new OutboxProcessingOptions();

        /// <summary>
        /// Defines the maximum batch size (number) of items that can be processed per execution;
        /// value of -1 disables batching and allow all items in the outbox to be processed.
        /// Note, this value must be balanced with the the number of items in the queue and the number
        /// of any possible items that are experiencing issues, such as blocking items in a Fifo group,
        /// because all if the Fifo group has a number of items pending greater than the batch size provided
        /// then the failing Fifo group will constitute the entire batch and nothing will be published until
        /// that blocking items are exhausted by failure due to Publishing Attempts or TTL.
        ///       
        /// </summary>
        public int ItemProcessingBatchSize { get; set; } = -1;

        /// <summary>
        /// Defines the maximum batch size (number) of items that can be processed per execution to Sql Server,
        /// all items will be processed but they will be transmitted in batches of this size; a value of -1 disables
        /// batching and allow all items to be sent in one single transmission to Sql Server.
        /// Note, this not the limit or max number of items to be updated; it works differently from
        /// ItemProcessingBatchSize in this regard because ALL items will be updated.
        /// </summary>
        public int ItemUpdatingBatchSize { get; set; } = 20;

        /// <summary>
        /// The Maximum number of publishing retry attempts that can be made before the item fails;
        /// value of -1 disables this and allows an infinite number of retry attempts.
        /// </summary>
        public int MaxPublishingAttempts { get; set; } = -1;

        /// <summary>
        /// The Maximum amount of time the item may exist before it is set as Expired;
        /// the Zero Timespan disables this and allows items to be retried for an infinite amount of time.
        /// </summary>
        public TimeSpan TimeSpanToLive { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Determine if we should enforce FIFO Publishing order which requires the use of
        /// a distributed application mutex lock to ensure that only one processor can execute
        /// at any time -- eliminating risk of parallel and potential impacts to processing order.
        /// </summary>
        public bool FifoEnforcedPublishingEnabled { get; set; } = false;

        /// <summary>
        /// An hook/callback for handling informational logging.
        /// </summary>
        public Action<string> LogDebugCallback { get; set; } = null;

        /// <summary>
        /// A hook/callback for handling error/exception logging.
        /// </summary>
        public Action<Exception> LogErrorCallback { get; set; } = null;
    }
}
