using System.Data;

namespace SqlTransactionalOutbox.SqlServer
{
    public readonly struct SqlOutboxTableColumnOrdinals
    {
        public SqlOutboxTableColumnOrdinals(
            // Cache ordinals once (faster than name lookups on every row)
            int createdDateTimeUtcOrdinal,
            int scheduledPublishDateTimeUtcOrdinal,
            int statusOrdinal,
            int fifoGroupingIdentifierOrdinal,
            int publishAttemptsOrdinal,
            int publishTargetOrdinal,
            int payloadOrdinal
        )
        {
            CreatedDateTimeUtcOrdinal = createdDateTimeUtcOrdinal;
            ScheduledPublishDateTimeUtcOrdinal = scheduledPublishDateTimeUtcOrdinal;
            StatusOrdinal = statusOrdinal;
            FifoGroupingIdentifierOrdinal = fifoGroupingIdentifierOrdinal;
            PublishAttemptsOrdinal = publishAttemptsOrdinal;
            PublishTargetOrdinal = publishTargetOrdinal;
            PayloadOrdinal = payloadOrdinal;
        }

        public int CreatedDateTimeUtcOrdinal { get; }
        public int ScheduledPublishDateTimeUtcOrdinal { get; }
        public int StatusOrdinal { get; }
        public int FifoGroupingIdentifierOrdinal { get; }
        public int PublishAttemptsOrdinal { get; }
        public int PublishTargetOrdinal { get; }
        public int PayloadOrdinal { get; }

        public static SqlOutboxTableColumnOrdinals FromSqlReaderRecord(IDataRecord record, ISqlTransactionalOutboxTableConfig outboxTableConfig)
            => new SqlOutboxTableColumnOrdinals(
                record.GetOrdinal(outboxTableConfig.CreatedDateTimeUtcFieldName),
                record.GetOrdinal(outboxTableConfig.ScheduledPublishDateTimeUtcFieldName),
                record.GetOrdinal(outboxTableConfig.StatusFieldName),
                record.GetOrdinal(outboxTableConfig.FifoGroupingIdentifier),
                record.GetOrdinal(outboxTableConfig.PublishAttemptsFieldName),
                record.GetOrdinal(outboxTableConfig.PublishTargetFieldName),
                record.GetOrdinal(outboxTableConfig.PayloadFieldName)
            );
    }
}
