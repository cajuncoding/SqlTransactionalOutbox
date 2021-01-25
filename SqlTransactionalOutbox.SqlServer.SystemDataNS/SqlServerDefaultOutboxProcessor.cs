using System;
using System.Data.SqlClient;

namespace SqlTransactionalOutbox.SqlServer.SystemDataNS
{
    public class SqlServerDefaultOutboxProcessor<TPayload>: OutboxProcessor<Guid, TPayload>, ISqlTransactionalOutboxProcessor<Guid, TPayload>
    {
        public SqlServerDefaultOutboxProcessor(
            SqlTransaction sqlTransaction,
            ISqlTransactionalOutboxPublisher<Guid> outboxPublisher,
            ISqlTransactionalOutboxRepository<Guid, TPayload> outboxRepository = null,
            int distributedMutexAcquisitionTimeoutSeconds = 5
        ) 
        : base (
            outboxRepository ?? new SqlServerDefaultOutboxRepository<TPayload>(
                sqlTransaction, 
                distributedMutexAcquisitionTimeoutSeconds: distributedMutexAcquisitionTimeoutSeconds
            ), 
            outboxPublisher
        )
        {}
    }
}
