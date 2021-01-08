using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace SqlTransactionalOutboxHelpers.SqlServer.SystemDataNS
{
    public class SqlServerOutboxProcessor : ISqlTransactionalOutboxProcessor
    {
        public SqlServerOutboxProcessor(SqlTransaction sqlTransaction)
        {
            
        }

        public Task<List<OutboxItem>> RetrieveOutboxItemsAsync()
        {
            throw new NotImplementedException();
        }

        public Task StoreOutboxItemAsync(OutboxItem outboxItem)
        {
            throw new NotImplementedException();
        }

        public bool IsTransactionValid()
        {
            throw new NotImplementedException();
        }
    }
}
