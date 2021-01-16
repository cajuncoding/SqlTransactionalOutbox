using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SqlTransactionalOutboxHelpers
{
    public interface ISqlTransactionalOutboxProcessor<TPayload>
    {
        Task<IEnumerable<ISqlTransactionalOutboxItem>> InsertNewPendingOutboxItemsAsync(IEnumerable<OutboxInsertItem<TPayload>> outboxInsertItems);

        Task<ISqlTransactionalOutboxItem> InsertNewPendingOutboxItemAsync(string publishingTarget, TPayload publishingPayload);

        Task<OutboxProcessingResults> ProcessPendingOutboxItemsAsync(
            OutboxProcessingOptions processingOptions = null,
            bool throwExceptionOnFailure = false
        );

        Task ProcessCleanupOfOutboxHistoricalItemsAsync(TimeSpan historyTimeToKeepTimeSpan);
    }
}
