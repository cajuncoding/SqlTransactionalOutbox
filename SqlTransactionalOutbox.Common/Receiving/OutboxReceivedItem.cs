using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.Interfaces;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class OutboxReceivedItem<TUniqueIdentifier, TPayload> : ISqlTransactionalOutboxReceivedMessage<TUniqueIdentifier, TPayload>
    {
        public bool IsReceiptAcknowledged { get; protected set; } = false;
        protected bool IsDisposed { get; set; } = false;

        public ISqlTransactionalOutboxItem<TUniqueIdentifier> ReceivedItem { get; }
        protected ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> OutboxItemFactory { get; }

        public OutboxReceivedItem(
            ISqlTransactionalOutboxItem<TUniqueIdentifier> receivedOutboxItem,
            ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> outboxItemFactory
        )
        {
            ReceivedItem = receivedOutboxItem.AssertNotNull(nameof(receivedOutboxItem));
            OutboxItemFactory = outboxItemFactory.AssertNotNull(nameof(outboxItemFactory));
        }

        public TPayload GetPayload()
        {
            var payload = OutboxItemFactory.ParsePayload(ReceivedItem);
            return payload;
        }
    }
}
