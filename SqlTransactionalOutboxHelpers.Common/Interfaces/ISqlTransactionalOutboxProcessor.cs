using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SqlTransactionalOutboxHelpers
{
    public interface ISqlTransactionalOutboxProcessor
    {
        Task<OutboxProcessingResults> ProcessPendingOutboxItemsAsync(
            OutboxProcessingOptions processingOptions = null,
            bool throwExceptionOnFailure = false
        );

        Task ProcessCleanupOfOutboxHistoricalItemsAsync(TimeSpan historyTimeToKeepTimeSpan);
    }
}
