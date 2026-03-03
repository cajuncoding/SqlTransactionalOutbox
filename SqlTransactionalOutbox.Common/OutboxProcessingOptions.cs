using System;

namespace SqlTransactionalOutbox
{
    public class OutboxProcessingOptions
    {
        //Provide Configuration Method for ease of use and discoverability; also allows for configuration via DI container
        //  registration in some cases (e.g. Azure Functions Startup class)
        public static OutboxProcessingOptions ConfigureDefaults(Action<OutboxProcessingOptions> configureOptionsAction)
        {
            var options = new OutboxProcessingOptions();
            configureOptionsAction?.Invoke(options);

            DefaultOutboxProcessingOptions = options;
            return DefaultOutboxProcessingOptions;
        }

        public static OutboxProcessingOptions DefaultOutboxProcessingOptions { get; set; } = new OutboxProcessingOptions();

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
        /// The allowable tolerance/deviation for retrieving Outbox Items before the exact Scheduled Publish Time.
        /// This provides support to fine-tune the balance between processing delays and actual Scheduled Publish Times
        ///     and is particularly useful when the Service Bus Publisher supports Scheduled/Deferred delivery such as Azure Service Bus!
        /// NOTE: The Default Value is Zero to preserve intuitive behaviour though many implementations will likely want to set this to reasonable
        ///     value greater than zero (e.g. 1 minute, 5 minutes, etc.) to support publishing to Azure Service Bus & delivering the event 
        ///     as close as possible to the actual scheduled time; more info. in below.
        /// NOTE: It's a good practice to leave messages pending in the Outbox as a reliable and easily observable persistence lcoation (in the SQL Database)
        ///     and then publish to the Azure Service Bus much closer to the actual scheduled delivery time -- prefetching only a couple minutes ahead.
        /// FOR EXAMPLE:
        ///     This is very helpful for implementations that run on a timer (e.g. Azure Functions Timer Trigger) where processing
        ///     may not occur at the exact scheduled time but may occur every 30 seconds (or 1 minute) in which case you could pre-fetch 
        ///     items that are scheduled to be published within the next 30 seconds (or 1 minute) to ensure they are published as close as possible
        ///     to their scheduled publish time.
        /// </summary>
        public TimeSpan ScheduledPublishPrefetchTime { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Determine if we should enforce FIFO Publishing order which requires the use of
        /// a distributed application mutex lock to ensure that only one processor can execute
        /// at any time -- eliminating risk of parallel processing and other potential impacts to processing order.
        /// </summary>
        public bool FifoEnforcedPublishingEnabled { get; set; } = false;

        /// <summary>
        /// An hook/callback for handling informational logging.
        /// </summary>
        public Action<string> LogDebugCallback { get; set; } = null;

        /// <summary>
        /// A hook/callback for handling error/exception logging.
        /// </summary>
        public Action<Exception> ErrorHandlerCallback { get; set; } = null;
    }
}
