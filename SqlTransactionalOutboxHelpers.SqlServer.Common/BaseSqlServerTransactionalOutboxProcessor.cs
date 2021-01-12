using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SqlTransactionalOutboxHelpers.CustomExtensions;

namespace SqlTransactionalOutboxHelpers.SqlServer.SystemDataNS
{
    public abstract class BaseSqlServerTransactionalOutboxProcessor : ISqlTransactionalOutboxProcessor
    {
        protected ISqlTransactionalOutboxProcessor OutboxProcessor { get; set; }

        protected void Init(ISqlTransactionalOutboxProcessor outboxProcessor)
        {
            this.OutboxProcessor = outboxProcessor.AssertNotNull(nameof(outboxProcessor));
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
        public Task ProcessCleanupOfOutboxHistoricalItemsAsync(TimeSpan historyTimeToKeepTimeSpan)
        {
            return OutboxProcessor.ProcessCleanupOfOutboxHistoricalItemsAsync(historyTimeToKeepTimeSpan);
        }
    }
}
