using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SqlTransactionalOutbox.Publishing;

namespace SqlTransactionalOutbox.IntegrationTests
{
    public class TestHarnessSqlTransactionalOutboxPublisher : SqlTransactionalOutboxDelegatePublisher<Guid>
    {
        public TestHarnessSqlTransactionalOutboxPublisher(
            Func<ISqlTransactionalOutboxItem<Guid>, bool, CancellationToken, Task> publishingAction = null
        ) : base(publishingAction ?? ((item, isFifoProcessingEnabled, cancellationToken) =>
            {
                Debug.WriteLine($"[{nameof(TestHarnessSqlTransactionalOutboxPublisher)}] PUBLISHING: {item.Payload}");
                return Task.CompletedTask;
            })
        )
        {
            //The base Func was initialized with default handler via Constructor chain...
        }
    }
}
