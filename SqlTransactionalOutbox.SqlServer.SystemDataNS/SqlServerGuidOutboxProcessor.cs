using System;
using System.Data.SqlClient;

namespace SqlTransactionalOutbox.SqlServer.SystemDataNS
{
    public class SqlServerGuidOutboxProcessor<TPayload>: OutboxProcessor<Guid, TPayload>, ISqlTransactionalOutboxProcessor<Guid, TPayload>
    {
        public SqlServerGuidOutboxProcessor(
            SqlTransaction sqlTransaction,
            ISqlTransactionalOutboxPublisher<Guid> outboxPublisher,
            ISqlTransactionalOutboxRepository<Guid, TPayload> outboxRepository = null,
            int distributedMutexAcquisitionTimeoutSeconds = 5
        ) 
        : base (
            outboxRepository ?? new SqlServerGuidOutboxRepository<TPayload>(
                sqlTransaction, 
                distributedMutexAcquisitionTimeoutSeconds: distributedMutexAcquisitionTimeoutSeconds
            ), 
            outboxPublisher
        )
        {}
    }
}
