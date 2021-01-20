using System;
using System.Data.SqlClient;

namespace SqlTransactionalOutboxHelpers.SqlServer.SystemDataNS
{
    public class SqlServerGuidTransactionalOutboxProcessor<TPayload>: OutboxProcessor<Guid, TPayload>, ISqlTransactionalOutboxProcessor<Guid, TPayload>
    {
        public SqlServerGuidTransactionalOutboxProcessor(
            SqlTransaction sqlTransaction,
            ISqlTransactionalOutboxPublisher<Guid> outboxPublisher,
            ISqlTransactionalOutboxRepository<Guid, TPayload> outboxRepository = null,
            int distributedMutexAcquisitionTimeoutSeconds = 5
        ) 
        : base (
            outboxRepository ?? new SqlServerGuidTransactionalOutboxRepository<TPayload>(
                sqlTransaction, 
                distributedMutexAcquisitionTimeoutSeconds: distributedMutexAcquisitionTimeoutSeconds
            ), 
            outboxPublisher
        )
        {}
    }
}
