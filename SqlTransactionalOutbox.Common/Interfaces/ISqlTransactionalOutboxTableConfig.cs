using System;

namespace SqlTransactionalOutbox
{
    public interface ISqlTransactionalOutboxTableConfig
    {
        /// <summary>
        /// The allowable tolerance/deviation for processing Outbox Items before or after their Scheduled Publish Time.
        /// This provides some support to fine-tune the balance between processing delays and actual Scheduled Publish Times, 
        ///     which can be helpful for implementations that run on a timer (e.g. Azure Functions Timer Trigger) where processing
        ///     may not occur at the exact scheduled time.
        /// </summary>
        TimeSpan ScheduledPublishTimeMarginOfError { get; }

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
