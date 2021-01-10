using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SqlTransactionalOutboxHelpers
{
    public interface ISqlTransactionalOutboxRepository
    {
        Task CreateOutboxItemAsync(ISqlTransactionalOutboxItem outboxItem);

        Task UpdateOutboxItemsAsync(List<ISqlTransactionalOutboxItem> outboxItem);

        Task<List<ISqlTransactionalOutboxItem>> RetrieveOutboxItemsAsync(OutboxItemStatus status, int maxBatchSize = -1);

        Task<IAsyncDisposable> AcquireDistributedProcessingMutexAsync();
    }
}
