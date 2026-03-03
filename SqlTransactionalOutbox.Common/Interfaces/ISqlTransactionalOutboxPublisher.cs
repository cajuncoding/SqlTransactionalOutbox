using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqlTransactionalOutbox
{
    public interface ISqlTransactionalOutboxPublisher<TUniqueIdentifier>: IAsyncDisposable
    {
        Task PublishOutboxItemAsync(
            ISqlTransactionalOutboxItem<TUniqueIdentifier> outboxItem, 
            bool isFifoEnforcedProcessingEnabled = false,
            CancellationToken cancellationToken = default
        );

    }
}
