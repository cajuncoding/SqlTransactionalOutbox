using System;
using System.Threading.Tasks;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox.Publishing
{
    public class SqlTransactionalOutboxDelegatePublisher<TUniqueIdentifier> : ISqlTransactionalOutboxPublisher<TUniqueIdentifier>
    {
        protected Func<ISqlTransactionalOutboxItem<TUniqueIdentifier>, bool, Task> PublishingDelegateFunc { get; }

        public SqlTransactionalOutboxDelegatePublisher(Func<ISqlTransactionalOutboxItem<TUniqueIdentifier>, bool, Task> publishingFunc)
        {
            PublishingDelegateFunc = publishingFunc.AssertNotNull(nameof(publishingFunc));
        }

        public Task PublishOutboxItemAsync(
            ISqlTransactionalOutboxItem<TUniqueIdentifier> outboxItem,
            bool isFifoEnforcedProcessingEnabled = false
        )
        {
            return PublishingDelegateFunc.Invoke(outboxItem, isFifoEnforcedProcessingEnabled);
        }

        public ValueTask DisposeAsync()
        {
            //Do Nothing
            return new ValueTask();
        }
    }
}
