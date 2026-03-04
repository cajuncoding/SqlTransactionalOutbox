using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SqlTransactionalOutbox
{
    public interface ISqlTransactionalOutboxRepository<TUniqueIdentifier, in TPayload>
    {
        Task<List<ISqlTransactionalOutboxItem<TUniqueIdentifier>>> InsertNewOutboxItemsAsync(
            IEnumerable<ISqlTransactionalOutboxInsertionItem<TPayload>> outboxItems, 
            int insertBatchSize = 20,
            CancellationToken cancellationToken = default
        );

        Task<List<ISqlTransactionalOutboxItem<TUniqueIdentifier>>> UpdateOutboxItemsAsync(
            IEnumerable<ISqlTransactionalOutboxItem<TUniqueIdentifier>> outboxItems, 
            int updateBatchSize = 20,
            CancellationToken cancellationToken = default
        );

        Task<List<ISqlTransactionalOutboxItem<TUniqueIdentifier>>> RetrieveOutboxItemsAsync(
            OutboxItemStatus status, 
            int maxBatchSize = -1,
            TimeSpan? scheduledPublishPrefetchTime = null,
            CancellationToken cancellationToken = default
        );

        Task CleanupOutboxHistoricalItemsAsync(TimeSpan historyTimeToKeepTimeSpan, CancellationToken cancellationToken = default);

        Task IncrementPublishAttemptsForAllItemsByStatusAsync(OutboxItemStatus status, CancellationToken cancellationToken = default);

        Task<IAsyncDisposable> AcquireDistributedProcessingMutexAsync(CancellationToken cancellationToken = default);
    }
}
