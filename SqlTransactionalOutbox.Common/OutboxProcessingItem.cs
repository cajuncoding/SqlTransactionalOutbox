using System;

namespace SqlTransactionalOutbox
{
    public class OutboxProcessingItem<TUniqueIdentifier> : ISqlTransactionalOutboxItem<TUniqueIdentifier>
    {
        /// <summary>
        /// A System generated UUID for the outbox; C# syntax for rendering GUID values should provide plenty
        /// of performance for the majority of implementations. In a micro-services world, where each application controls
        /// it's own outbox, and with the use of Sql Server IDENTITY, then there is not a need for anything advanced like
        /// Snowflake Ids for the majority of use cases; but this can be fully customized if necessary
        /// via ISqlTransactionalOutboxItemFactory & corresponding ISqlTransactionalOutboxUniqueIdFactory
        /// implementations to override the generation of ID as a GUID.
        /// </summary>
        public TUniqueIdentifier UniqueIdentifier { get; set; }

        /// <summary>
        /// A user specified unique ID, in string form, for outbox items that is used to isolate groups of items when
        /// FIFO enforced processing is enabled.  This identifier allows items in different groups to still be processed
        /// even when there is an issue (e.g. publishing exceptions) with one specific group as defined by this identifier.
        /// So potential blocking issues may be isolated at the grouping level instead of universally for the entire outbox.
        /// Note: Conceptually, this functions similarly to how `SessionId` works with Azure Service Bus messages for FIFO delivery.
        /// </summary>
        public string FifoGroupingIdentifier { get; set; }

        /// <summary>
        /// Status of the Outbox item (e.g. Pending, Successful, Failed*).
        /// </summary>
        public OutboxItemStatus Status { get; set; }

        /// <summary>
        /// The target for which the outbox item will be published to; this is usually a Message Bus Topic,
        /// but may also represent any other conceptual destination to be interpreted and handled by the
        /// ISqlTransactionalOutboxPublisher.
        /// </summary>
        public string PublishTarget { get; set; }

        /// <summary>
        /// The Serialized Payload for the ISqlTransactionalOutboxPublisher to handle; it can be any format, but
        /// generally a Json serialized payload containing the message content as well as additional metadata, headers, etc.
        /// will be serialized and stored as the payload
        /// </summary>
        public string Payload { get; set; }

        /// <summary>
        /// The number of Attempts that have been made to publish this item.
        /// </summary>
        public int PublishAttempts { get; set; }

        /// <summary>
        /// Exact UTC Date & Time this Outbox Item was created; needs to be highly exact to help ensure FIFO ordered processing.
        /// NOTE: Ideally this is set at the Database Level and not at the Application Level;
        ///         in SQL Server we can do this with DEFAULT Values to ensure that times are consistently
        ///         derived with no risk of being out-of-sync from parallel processes.
        /// </summary>
        public DateTimeOffset CreatedDateTimeUtc { get; set; }

        /// <summary>
        /// Exact UTC Date & Time this Outbox Item is scheduled to be published; needs to be highly exact to help ensure FIFO ordered processing.
        /// The delivery may not occur exactly at this time due to various factors (e.g. processing delays, etc.), but the precision of this 
        ///     value is important to ensure that items are published in the correct order.
        /// For example, within Azure Functions the Outbox likely runs on a Timer Trigger with a certain frequency (e.g. every 5 seconds) 
        ///     it's unlikely they will be processed at the exact scheduled time. But they will be published within the timer iteration as the margin of error (e.g. +/- 5 seconds).
        /// To provide better control the Outbox configuration options can be used to provide a buffer window (e.g. 10 seconds, or 30 seconds) to 
        ///     for +/- processing allowing for items to be processed even slightly before or afte their scheduled time. 
        ///     The default for this however is 0 seconds, meaning items will only be processed once their scheduled time has passed.
        /// </summary>
        public DateTimeOffset? ScheduledPublishDateTimeUtc { get; set; }
    }
}
