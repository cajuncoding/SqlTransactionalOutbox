using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SqlTransactionalOutbox
{
    public interface ISqlTransactionalOutbox<TUniqueIdentifier, in TPayload>
    {
        Task<ISqlTransactionalOutboxItem<TUniqueIdentifier>> InsertNewPendingOutboxItemAsync(
            string publishingTarget, 
            TPayload publishingPayload,
            string fifoGroupingIdentifier = null,
            DateTimeOffset? scheduledPublishDateTimeUtc = null
        );

        Task<List<ISqlTransactionalOutboxItem<TUniqueIdentifier>>> InsertNewPendingOutboxItemsAsync(
            IEnumerable<ISqlTransactionalOutboxInsertionItem<TPayload>> outboxInsertionItems
        );

        Task CleanupHistoricalOutboxItemsAsync(TimeSpan historyTimeToKeepTimeSpan);
    }
}
