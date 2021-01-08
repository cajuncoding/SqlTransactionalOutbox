using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SqlTransactionalOutboxHelpers
{
    public interface ISqlTransactionalOutboxProcessor
    {
        Task<List<OutboxItem>> RetrieveOutboxItemsAsync();

        Task StoreOutboxItemAsync(OutboxItem outboxItem);

        bool IsTransactionValid();
    }
}
