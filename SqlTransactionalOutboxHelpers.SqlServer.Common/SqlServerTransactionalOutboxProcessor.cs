using System;

namespace SqlTransactionalOutboxHelpers.SqlServer.SystemDataNS
{
    public class SqlServerTransactionalOutboxProcessor<TPayload> : OutboxProcessor<TPayload>, ISqlTransactionalOutboxProcessor<TPayload>
    {
        public SqlServerTransactionalOutboxProcessor(
            ISqlTransactionalOutboxRepository outboxRepository,
            ISqlTransactionalOutboxPublisher outboxPublisher)
            : base(outboxRepository, outboxPublisher)
        {}
    }
}
