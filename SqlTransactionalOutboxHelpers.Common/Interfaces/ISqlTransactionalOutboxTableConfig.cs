using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutboxHelpers
{
    public interface ISqlTransactionalOutboxTableConfig
    {
        string TransactionalOutboxSchemaName { get; }
        
        string TransactionalOutboxTableName { get; }

        string PKeyFieldName { get; }
        
        string UniqueIdentifierFieldName { get; }
        
        string StatusFieldName { get; }
        
        string PublishingTargetFieldName { get; }
        
        string PublishingPayloadFieldName { get; }

        string PublishingAttemptsFieldName { get; }

        string CreatedDateTimeUtcFieldName { get; }
    }
}
