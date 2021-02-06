using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.Utilities;

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
            ISqlTransactionalOutboxRepository<TUniqueIdentifier, TPayload> outboxRepository
        )
        {
            this.OutboxRepository = outboxRepository ?? throw new ArgumentNullException(nameof(OutboxRepository));
        }

        public virtual async Task<ISqlTransactionalOutboxItem<TUniqueIdentifier>> InsertNewPendingOutboxItemAsync(
            string publishingTarget, 
            TPayload publishingPayload
        )
        {
            //Use the Outbox Item Factory to create a new Outbox Item (serialization of the Payload will be handled by the Factory).
            var outboxInsertItem = new OutboxInsertionItem<TPayload>(publishingTarget, publishingPayload);

            //Store the outbox item using the Repository...
            var resultItems = await InsertNewPendingOutboxItemsAsync(
                new List<ISqlTransactionalOutboxInsertionItem<TPayload>>() { outboxInsertItem }
            ).ConfigureAwait(false);

            return resultItems.FirstOrDefault();
        }

        public virtual async Task<List<ISqlTransactionalOutboxItem<TUniqueIdentifier>>> InsertNewPendingOutboxItemsAsync(
            IEnumerable<ISqlTransactionalOutboxInsertionItem<TPayload>> outboxInsertionItems
        )
        {
            //Store the outbox item using the Repository...
            var resultItems = await OutboxRepository.InsertNewOutboxItemsAsync(outboxInsertionItems).ConfigureAwait(false);

            return resultItems;
        }
    }
}
