using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;

namespace SqlTransactionalOutbox.SqlServer.SystemDataNS
{
    public class DefaultSqlServerOutboxRepository<TPayload> : SqlServerOutboxRepository<Guid, TPayload>
    {
        public DefaultSqlServerOutboxRepository(
            SqlTransaction sqlTransaction,
            ISqlTransactionalOutboxTableConfig outboxTableConfig = null,
            ISqlTransactionalOutboxItemFactory<Guid, TPayload> outboxItemFactory = null,
            int distributedMutexAcquisitionTimeoutSeconds = 5
        ) : base (
            sqlTransaction: sqlTransaction,
            outboxTableConfig: outboxTableConfig ?? new OutboxTableConfig(),
            outboxItemFactory: outboxItemFactory ?? new OutboxItemFactory<Guid, TPayload>(
                new OutboxGuidUniqueIdentifier()
            ), 
            distributedMutexAcquisitionTimeoutSeconds
        )
        {
            //Preconfigured defaults are used in base constructor above...
        }
    }
}
