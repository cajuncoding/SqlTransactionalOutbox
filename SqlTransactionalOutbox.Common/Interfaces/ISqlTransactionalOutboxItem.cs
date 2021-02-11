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
    }
}