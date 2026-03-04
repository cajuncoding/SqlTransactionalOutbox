using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SqlTransactionalOutbox
{
    /// <summary>
    /// Process all pending items in the transactional outbox using the specified ISqlTransactionalOutboxRepository,
    /// ISqlTransactionalOutboxPublisher, & OutboxProcessingOptions
    /// </summary>
    public class TransactionalOutbox<TUniqueIdentifier, TPayload> : ISqlTransactionalOutbox<TUniqueIdentifier, TPayload>
    {
        public ISqlTransactionalOutboxRepository<TUniqueIdentifier, TPayload> OutboxRepository { get; }

        public TransactionalOutbox(
            ISqlTransactionalOutboxRepository<TUniqueIdentifier, TPayload> outboxRepository,
            CancellationToken cancellationToken = default
        )
        {
            this.OutboxRepository = outboxRepository ?? throw new ArgumentNullException(nameof(OutboxRepository));
        }

        public virtual async Task<ISqlTransactionalOutboxItem<TUniqueIdentifier>> InsertNewPendingOutboxItemAsync(
            string publishingTarget, 
            TPayload publishingPayload,
            string fifoGroupingIdentifier = null,
            DateTimeOffset? scheduledPublishDateTimeUtc = null,
            CancellationToken cancellationToken = default
        )
        {
            //Store the outbox item using the Repository...
            var resultItems = await InsertNewPendingOutboxItemsAsync(
                new List<ISqlTransactionalOutboxInsertionItem<TPayload>>()
                {
                    new OutboxInsertionItem<TPayload>(publishingTarget, publishingPayload, fifoGroupingIdentifier, scheduledPublishDateTimeUtc)
                },
                cancellationToken
            ).ConfigureAwait(false);

            return resultItems.FirstOrDefault();
        }

        public virtual async Task<List<ISqlTransactionalOutboxItem<TUniqueIdentifier>>> InsertNewPendingOutboxItemsAsync(
            IEnumerable<ISqlTransactionalOutboxInsertionItem<TPayload>> outboxInsertionItems,
            CancellationToken cancellationToken = default
        )
        {
            //Store the outbox item using the Repository...
            var resultItems = await OutboxRepository
                .InsertNewOutboxItemsAsync(outboxInsertionItems, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            
            return resultItems;
        }

        public virtual async Task CleanupHistoricalOutboxItemsAsync(TimeSpan historyTimeToKeepTimeSpan, CancellationToken cancellationToken = default)
        {
            //Cleanup the Historical data using the Repository...
            await OutboxRepository.CleanupOutboxHistoricalItemsAsync(historyTimeToKeepTimeSpan, cancellationToken).ConfigureAwait(false);
        }
    }
}
