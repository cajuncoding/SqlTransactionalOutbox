using System;
using System.Threading.Tasks;
using SqlTransactionalOutboxHelpers.CustomExtensions;

namespace SqlTransactionalOutboxHelpers
{
    public class SqlTransactionalOutboxDelegatePublisher<TUniqueIdentifier> : ISqlTransactionalOutboxPublisher<TUniqueIdentifier>
    {
        protected Func<ISqlTransactionalOutboxItem<TUniqueIdentifier>, Task> PublishingDelegateFunc { get; }

        public SqlTransactionalOutboxDelegatePublisher(Func<ISqlTransactionalOutboxItem<TUniqueIdentifier>, Task> publishingFunc)
        {
            PublishingDelegateFunc = publishingFunc.AssertNotNull(nameof(publishingFunc));
        }
        public Task PublishOutboxItemAsync(ISqlTransactionalOutboxItem<TUniqueIdentifier> outboxItem)
        {
            return PublishingDelegateFunc.Invoke(outboxItem);
        }
    }
}
