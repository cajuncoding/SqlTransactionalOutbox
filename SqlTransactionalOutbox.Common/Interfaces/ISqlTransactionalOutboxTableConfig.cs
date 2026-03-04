using System;

namespace SqlTransactionalOutbox
{
    public interface ISqlTransactionalOutboxTableConfig
    {
        string TransactionalOutboxSchemaName { get; }
        
        string TransactionalOutboxTableName { get; }

        string PKeyFieldName { get; }
        
        string UniqueIdentifierFieldName { get; }

        string FifoGroupingIdentifier { get; }

        string StatusFieldName { get; }
        
        string PublishTargetFieldName { get; }
        
        string PayloadFieldName { get; }

        string PublishAttemptsFieldName { get; }

        string CreatedDateTimeUtcFieldName { get; }

        string ScheduledPublishDateTimeUtcFieldName { get; }
    }
}
