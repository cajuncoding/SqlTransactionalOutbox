using System;
using System.Data.SqlClient;
using SqlTransactionalOutbox.SqlServer.Common;

namespace SqlTransactionalOutbox.SqlServer.SystemDataNS
{
    public class DefaultSqlServerTransactionalOutboxProcessor<TPayload> : TransactionalOutboxProcessor<Guid, TPayload>
    {
        public DefaultSqlServerTransactionalOutboxProcessor(
            SqlTransaction sqlTransaction,
            ISqlTransactionalOutboxPublisher<Guid> outboxPublisher,
            int distributedMutexAcquisitionTimeoutSeconds = Defaults.DistributedMutexAcquisitionTimeoutSeconds
        ) 
        : base (
            new DefaultSqlServerOutboxRepository<TPayload>(sqlTransaction, distributedMutexAcquisitionTimeoutSeconds: distributedMutexAcquisitionTimeoutSeconds), 
            outboxPublisher
        )
        {}
    }
}
