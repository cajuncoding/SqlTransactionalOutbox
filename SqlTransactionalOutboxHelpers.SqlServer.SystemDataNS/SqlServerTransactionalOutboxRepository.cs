using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace SqlTransactionalOutboxHelpers.SqlServer.SystemDataNS
{
    public class SqlServerTransactionalOutboxRepository : ISqlTransactionalOutboxRepository
    {
        public SqlServerTransactionalOutboxRepository(SqlTransaction sqlTransaction)
        {
            
        }

        public virtual async Task<List<OutboxItem>> RetrievePendingOutboxItemsAsync(int maxBatchSize = -1)
        {
            throw new NotImplementedException();
        }

        public virtual async Task CreateOutboxItemAsync(OutboxItem outboxItem)
        {
            throw new NotImplementedException();
        }

        public virtual async Task UpdateOutboxItemsAsync(List<OutboxItem> outboxItem)
        {
            throw new NotImplementedException();
        }

        public virtual async Task<IAsyncDisposable> AcquireDistributedProcessingMutexAsync()
        {
            throw new NotImplementedException();
        }

    }
}
