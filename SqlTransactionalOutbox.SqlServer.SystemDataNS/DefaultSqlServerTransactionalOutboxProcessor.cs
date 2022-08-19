using System;
using System.Data.SqlClient;

namespace SqlTransactionalOutbox.SqlServer.SystemDataNS
{
    public class DefaultSqlServerTransactionalOutboxProcessor<TPayload> : TransactionalOutboxProcessor<Guid, TPayload>
    {
        public DefaultSqlServerTransactionalOutboxProcessor(
            SqlTransaction sqlTransaction,
            ISqlTransactionalOutboxPublisher<Guid> outboxPublisher,
            int? distributedMutexAcquisitionTimeoutSeconds = null
        ) 
        : base (
            new DefaultSqlServerOutboxRepository<TPayload>(sqlTransaction, distributedMutexAcquisitionTimeoutSeconds: distributedMutexAcquisitionTimeoutSeconds), 
            outboxPublisher
        )
        {}
    }
}
