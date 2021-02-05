using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace SqlTransactionalOutbox.IntegrationTests
{
    public class TestHarnessSqlTransactionalOutboxPublisher : SqlTransactionalOutboxDelegatePublisher<Guid>
    {
        public TestHarnessSqlTransactionalOutboxPublisher(
            Func<ISqlTransactionalOutboxItem<Guid>, bool, Task> publishingAction = null
        )
        : base(publishingAction ?? ((i, isFifoProcessingEnabled) =>
            {
                Debug.WriteLine($"[{nameof(TestHarnessSqlTransactionalOutboxPublisher)}] PUBLISHING: {i.Payload}");
                return Task.CompletedTask;
            })
        )
        {
            //The base Func was initialized with default handler via Constructor chain...
        }
    }
}
