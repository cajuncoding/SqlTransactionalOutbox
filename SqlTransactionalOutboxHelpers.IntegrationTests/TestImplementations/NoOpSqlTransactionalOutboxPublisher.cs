using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace SqlTransactionalOutboxHelpers.IntegrationTests
{
    public class NoOpSqlTransactionalOutboxPublisher : ISqlTransactionalOutboxPublisher<Guid>
    {
        public Task PublishOutboxItemAsync(ISqlTransactionalOutboxItem<Guid> outboxItem)
        {
            Debug.WriteLine($"[{nameof(NoOpSqlTransactionalOutboxPublisher)}] PUBLISHING: {outboxItem.PublishingPayload}");
            return Task.CompletedTask;
        }
    }
}
