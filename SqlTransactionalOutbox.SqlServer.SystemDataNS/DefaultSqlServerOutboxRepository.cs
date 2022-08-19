using System;
using System.Data.SqlClient;

namespace SqlTransactionalOutbox.SqlServer.SystemDataNS
{
    public class DefaultSqlServerOutboxRepository<TPayload> : SqlServerOutboxRepository<Guid, TPayload>
    {
        public DefaultSqlServerOutboxRepository(
            SqlTransaction sqlTransaction,
            ISqlTransactionalOutboxTableConfig outboxTableConfig = null,
            ISqlTransactionalOutboxItemFactory<Guid, TPayload> outboxItemFactory = null,
            int? distributedMutexAcquisitionTimeoutSeconds = null
        ) 
        : base (
            sqlTransaction: sqlTransaction,
            outboxTableConfig: outboxTableConfig ?? new OutboxTableConfig(),
            outboxItemFactory: outboxItemFactory ?? new DefaultOutboxItemFactory<TPayload>(), 
            distributedMutexAcquisitionTimeoutSeconds
        )
        {
            //Preconfigured defaults are used in base constructor above...
        }
    }
}
