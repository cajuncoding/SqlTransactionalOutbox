using System;
using System.Data.SqlClient;

namespace SqlTransactionalOutboxHelpers.SqlServer.SystemDataNS
{
    public class SqlServerTransactionalOutboxProcessor<TPayload>: OutboxProcessor<Guid, TPayload>, ISqlTransactionalOutboxProcessor<Guid, TPayload>
    {
        public SqlServerTransactionalOutboxProcessor(
            SqlTransaction sqlTransaction,
            ISqlTransactionalOutboxPublisher<Guid> outboxPublisher,
            ISqlTransactionalOutboxRepository<Guid, TPayload> outboxRepository = null
        ) 
        : base (
            outboxRepository ?? new SqlServerTransactionalOutboxRepository<TPayload>(sqlTransaction), 
            outboxPublisher
        )
        {}
    }
}
