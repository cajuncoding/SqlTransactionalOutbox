using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SqlTransactionalOutboxHelpers
{
    public interface ISqlTransactionalOutboxRepository
    {
        Task CreateOutboxItemAsync(OutboxItem outboxItem);

        Task UpdateOutboxItemsAsync(List<OutboxItem> outboxItem);

        Task<List<OutboxItem>> RetrievePendingOutboxItemsAsync(int maxBatchSize = -1);

        Task<IAsyncDisposable> AcquireDistributedProcessingMutexAsync();
    }
}
