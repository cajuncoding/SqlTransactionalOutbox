using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace SqlTransactionalOutboxHelpers.IntegrationTests
{
    public class TestHarnessSqlTransactionalOutboxPublisher : SqlTransactionalOutboxDelegatePublisher<Guid>
    {
        public TestHarnessSqlTransactionalOutboxPublisher(
            Func<ISqlTransactionalOutboxItem<Guid>, Task> publishingAction = null
        )
        : base(publishingAction ?? (i =>
            {
                Debug.WriteLine($"[{nameof(TestHarnessSqlTransactionalOutboxPublisher)}] PUBLISHING: {i.PublishingPayload}");
                return Task.CompletedTask;
            })
        )
        {
            //The base Func was initialized with default handler via Constructor chain...
        }
    }
}
