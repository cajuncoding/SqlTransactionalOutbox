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

        protected Func<ISqlTransactionalOutboxItem<TUniqueIdentifier>, TPayload> ParsePayloadFunc { get; }
        protected Func<Task> AcknowledgeReceiptAsyncFunc { get; }
        protected Func<Task> RejectReceiptAsyncFunc { get; }

        public OutboxReceivedItem(
            ISqlTransactionalOutboxItem<TUniqueIdentifier> receivedOutboxItem,
            Func<Task> acknowledgeReceiptAsyncFunc,
            Func<Task> rejectReceiptAsyncFunc,
            Func<ISqlTransactionalOutboxItem<TUniqueIdentifier>, TPayload> parsePayloadFunc
        )
        {
            ReceivedItem = receivedOutboxItem.AssertNotNull(nameof(receivedOutboxItem));
            AcknowledgeReceiptAsyncFunc = acknowledgeReceiptAsyncFunc.AssertNotNull(nameof(acknowledgeReceiptAsyncFunc)); ;
            RejectReceiptAsyncFunc = rejectReceiptAsyncFunc.AssertNotNull(nameof(rejectReceiptAsyncFunc)); ;
            ParsePayloadFunc = parsePayloadFunc.AssertNotNull(nameof(parsePayloadFunc)); ;
        }

        public TPayload GetPayload()
        {
            return ParsePayloadFunc(ReceivedItem);
        }

        public virtual async Task AcknowledgeReceiptAsync()
        {
            await AcknowledgeReceiptAsyncFunc().ConfigureAwait(false);
            IsReceiptAcknowledged = true;
        }

        public virtual async Task RejectReceiptAsync()
        {
            await RejectReceiptAsyncFunc().ConfigureAwait(false);
        }

        public virtual async ValueTask DisposeAsync()
        {
            //Always Reject if the task has not yet been acknowledged;
            // also ensure that the Dispose is re-entrant safe...
            if (!IsDisposed && !IsReceiptAcknowledged)
            {
                await RejectReceiptAsync();
            }
        }
    }
}
