using System;
using System.Data.SqlClient;

namespace SqlTransactionalOutbox.SqlServer.SystemDataNS
{
    public class DefaultSqlServerOutboxProcessor<TPayload>: OutboxProcessor<Guid, TPayload>, ISqlTransactionalOutboxProcessor<Guid, TPayload>
    {
        public DefaultSqlServerOutboxProcessor(
            SqlTransaction sqlTransaction,
            ISqlTransactionalOutboxPublisher<Guid> outboxPublisher,
            ISqlTransactionalOutboxRepository<Guid, TPayload> outboxRepository = null,
            int distributedMutexAcquisitionTimeoutSeconds = 5
        ) 
        : base (
            outboxRepository ?? new DefaultSqlServerOutboxRepository<TPayload>(
                sqlTransaction, 
                distributedMutexAcquisitionTimeoutSeconds: distributedMutexAcquisitionTimeoutSeconds
            ), 
            outboxPublisher
        )
        {}
    }
}
