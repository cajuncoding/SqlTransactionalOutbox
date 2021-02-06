using System;
using System.Data.SqlClient;
using SqlTransactionalOutbox.SqlServer.Common;

namespace SqlTransactionalOutbox.SqlServer.SystemDataNS
{
    public class DefaultSqlServerOutboxRepository<TPayload> : SqlServerOutboxRepository<Guid, TPayload>
    {
        public DefaultSqlServerOutboxRepository(
            SqlTransaction sqlTransaction,
            ISqlTransactionalOutboxTableConfig outboxTableConfig = null,
            ISqlTransactionalOutboxItemFactory<Guid, TPayload> outboxItemFactory = null,
            int distributedMutexAcquisitionTimeoutSeconds = Defaults.DistributedMutexAcquisitionTimeoutSeconds
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
