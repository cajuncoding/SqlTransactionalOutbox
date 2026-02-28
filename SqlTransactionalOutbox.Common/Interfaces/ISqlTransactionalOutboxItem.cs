using System;

namespace SqlTransactionalOutbox
{
    public interface ISqlTransactionalOutboxItem<TUniqueIdentifier>
    {
        /// <summary>
        /// A System generated UUID for the outbox; C# syntax for rendering GUID's should provide plenty
        /// of performance for the majority of implementations. Rarely is there a need for anything advanced like Snowflake Ids,
        /// but if necessary an ISqlTransactionalOutboxItemFactory may be implemented to override the generation of ID as a GUID.
        /// </summary>
        TUniqueIdentifier UniqueIdentifier { get; set; }

        /// <summary>
        /// A System generated UUID for the outbox; C# syntax for rendering GUID's should provide plenty
        /// of performance for the majority of implementations. Rarely is there a need for anything advanced like Snowflake Ids,
        /// but if necessary an ISqlTransactionalOutboxItemFactory may be implemented to override the generation of ID as a GUID.
        /// </summary>
        string FifoGroupingIdentifier { get; set; }

        /// <summary>
        /// Status of the Outbox item (e.g. Pending, Successful, Failed*).
        /// </summary>
        OutboxItemStatus Status { get; set; }

        /// <summary>
        /// The target for which the outbox item will be published to; this is usually a Message Bus Topic,
        /// but may also represent any other conceptual destination to be interpreted and handled by the
        /// ISqlTransactionalOutboxPublisher.
        /// </summary>
        string PublishTarget { get; set; }

        /// <summary>
        /// The number of Attempts that have been made to publish this item.
        /// </summary>
        int PublishAttempts { get; set; }

        /// <summary>
        /// The Serialized Payload for the ISqlTransactionalOutboxPublisher to handle; it can be any format, but
        /// generally a Json serialized payload containing the message content as well as additional metadata, headers, etc.
        /// will be serialized and stored as the payload
        /// </summary>
        string Payload { get; set; }

        /// <summary>
        /// Exact UTC Date & Time this Outbox Item was created; needs to be highly exact to help ensure FIFO ordered processing.
        /// NOTE: Ideally this is set at the Database Level and not at the Application Level;
        ///         in SQL Server we can do this with DEFAULT Values to ensure that times are consistently
        ///         derived with no risk of being out-of-sync from parallel processes.
        /// </summary>
        DateTimeOffset CreatedDateTimeUtc { get; set; }

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
        DateTimeOffset? ScheduledPublishDateTimeUtc { get; set; }
    }
}