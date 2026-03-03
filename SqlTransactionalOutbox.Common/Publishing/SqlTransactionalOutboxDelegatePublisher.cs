using System;
using System.Threading;
using System.Threading.Tasks;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox.Publishing
{
    public class SqlTransactionalOutboxDelegatePublisher<TUniqueIdentifier> : ISqlTransactionalOutboxPublisher<TUniqueIdentifier>
    {
        protected Func<ISqlTransactionalOutboxItem<TUniqueIdentifier>, bool, CancellationToken, Task> PublishingDelegateFunc { get; }

        public SqlTransactionalOutboxDelegatePublisher(Func<ISqlTransactionalOutboxItem<TUniqueIdentifier>, bool, CancellationToken, Task> publishingFunc)
        {
            PublishingDelegateFunc = publishingFunc.AssertNotNull(nameof(publishingFunc));
        }

        public Task PublishOutboxItemAsync(
            ISqlTransactionalOutboxItem<TUniqueIdentifier> outboxItem,
            bool isFifoEnforcedProcessingEnabled = false,
            CancellationToken cancellationToken = default
        )
        {
            return PublishingDelegateFunc.Invoke(outboxItem, isFifoEnforcedProcessingEnabled, cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            //Do Nothing
            return new ValueTask();
        }
    }
}
