using System.Threading;
using System.Threading.Tasks;

namespace SqlTransactionalOutbox
{
    public interface ISqlTransactionalOutboxProcessor<TUniqueIdentifier>
    {
        Task<ISqlTransactionalOutboxProcessingResults<TUniqueIdentifier>> ProcessPendingOutboxItemsAsync(
            OutboxProcessingOptions processingOptions = null,
            bool throwExceptionOnFailure = false,
            CancellationToken cancellationToken = default
        );

    }
}
