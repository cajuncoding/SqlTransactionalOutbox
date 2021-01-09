using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;

namespace SqlTransactionalOutboxHelpers.SqlServer.SystemDataNS
{
    public class SqlServerTransactionalOutboxProcessor : ISqlTransactionalOutboxProcessor
    {
        protected ISqlTransactionalOutboxProcessor OutboxProcessor { get; }

        public SqlServerTransactionalOutboxProcessor(
            SqlTransaction sqlTransaction,
            ISqlTransactionalOutboxPublisher outboxPublisher)
        {
            //Initialize Sql Server repository and Outbox Processor with needed dependencies
            var sqlServerOutboxRepository = new SqlServerTransactionalOutboxRepository(sqlTransaction);
            this.OutboxProcessor = new OutboxProcessor(sqlServerOutboxRepository, outboxPublisher);
        }

        public async Task<OutboxProcessingResults> ProcessPendingOutboxItemsAsync(
            OutboxProcessingOptions processingOptions = null,
            bool throwExceptionOnFailure = false
        )
        {
            //Delegate to the base processor...
            var results = await this.OutboxProcessor.ProcessPendingOutboxItemsAsync(processingOptions, throwExceptionOnFailure);
            return results;
        }
    }
}
