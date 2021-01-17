using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SqlTransactionalOutboxHelpers
{
    public interface ISqlTransactionalOutboxRepository<TUniqueIdentifier, TPayload>
    {
        Task<IEnumerable<ISqlTransactionalOutboxItem<TUniqueIdentifier>>> InsertNewOutboxItemsAsync(
            IEnumerable<ISqlTransactionalOutboxInsertionItem<TPayload>> outboxItems, 
            int insertBatchSize = 20
        );

        Task<IEnumerable<ISqlTransactionalOutboxItem<TUniqueIdentifier>>> UpdateOutboxItemsAsync(
            IEnumerable<ISqlTransactionalOutboxItem<TUniqueIdentifier>> outboxItems, 
            int updateBatchSize = 20
        );

        Task<List<ISqlTransactionalOutboxItem<TUniqueIdentifier>>> RetrieveOutboxItemsAsync(
            OutboxItemStatus status, 
            int maxBatchSize = -1
        );

        Task CleanupOutboxHistoricalItemsAsync(TimeSpan historyTimeToKeepTimeSpan);

        Task<IAsyncDisposable> AcquireDistributedProcessingMutexAsync();
    }
}
