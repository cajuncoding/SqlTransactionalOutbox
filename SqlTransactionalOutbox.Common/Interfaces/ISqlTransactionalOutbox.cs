using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SqlTransactionalOutbox
{
    public interface ISqlTransactionalOutbox<TUniqueIdentifier, in TPayload>
    {
        Task<ISqlTransactionalOutboxItem<TUniqueIdentifier>> InsertNewPendingOutboxItemAsync(
            string publishingTarget, 
            TPayload publishingPayload,
            string fifoGroupingIdentifier = null,
            DateTimeOffset? scheduledPublishDateTimeUtc = null,
            CancellationToken cancellationToken = default
        );

        Task<List<ISqlTransactionalOutboxItem<TUniqueIdentifier>>> InsertNewPendingOutboxItemsAsync(
            IEnumerable<ISqlTransactionalOutboxInsertionItem<TPayload>> outboxInsertionItems,
            CancellationToken cancellationToken = default
        );

        Task CleanupHistoricalOutboxItemsAsync(TimeSpan historyTimeToKeepTimeSpan, CancellationToken cancellationToken = default);
    }
}
