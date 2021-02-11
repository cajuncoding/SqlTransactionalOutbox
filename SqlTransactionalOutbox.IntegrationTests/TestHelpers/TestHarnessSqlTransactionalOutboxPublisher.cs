using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using SqlTransactionalOutbox.Publishing;

namespace SqlTransactionalOutbox.IntegrationTests
{
    public class TestHarnessSqlTransactionalOutboxPublisher : SqlTransactionalOutboxDelegatePublisher<Guid>
    {
        public TestHarnessSqlTransactionalOutboxPublisher(
            Func<ISqlTransactionalOutboxItem<Guid>, bool, Task> publishingAction = null
        )
        : base(publishingAction ?? ((item, isFifoProcessingEnabled) =>
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
