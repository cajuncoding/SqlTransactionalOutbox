using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutbox
{
    public interface ISqlTransactionalOutboxTableConfig
    {
        string TransactionalOutboxSchemaName { get; }
        
        string TransactionalOutboxTableName { get; }

        string PKeyFieldName { get; }
        
        string UniqueIdentifierFieldName { get; }
        
        string StatusFieldName { get; }
        
        string PublishTargetFieldName { get; }
        
        string PayloadFieldName { get; }

        string PublishAttemptsFieldName { get; }

        string CreatedDateTimeUtcFieldName { get; }
    }
}
