using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SqlTransactionalOutbox
{
    public interface ISqlTransactionalOutboxRepository<TUniqueIdentifier, in TPayload>
    {
        Task<List<ISqlTransactionalOutboxItem<TUniqueIdentifier>>> InsertNewOutboxItemsAsync(
            IEnumerable<ISqlTransactionalOutboxInsertionItem<TPayload>> outboxItems, 
            int insertBatchSize = 20
        );

        Task<List<ISqlTransactionalOutboxItem<TUniqueIdentifier>>> UpdateOutboxItemsAsync(
            IEnumerable<ISqlTransactionalOutboxItem<TUniqueIdentifier>> outboxItems, 
            int updateBatchSize = 20
        );

        Task<List<ISqlTransactionalOutboxItem<TUniqueIdentifier>>> RetrieveOutboxItemsAsync(
            OutboxItemStatus status, 
            int maxBatchSize = -1
        );

        Task CleanupOutboxHistoricalItemsAsync(TimeSpan historyTimeToKeepTimeSpan);

        Task IncrementPublishAttemptsForAllItemsByStatusAsync(OutboxItemStatus status);

        Task<IAsyncDisposable> AcquireDistributedProcessingMutexAsync();
    }
}
