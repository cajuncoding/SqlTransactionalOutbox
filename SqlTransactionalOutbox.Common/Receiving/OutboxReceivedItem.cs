using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.Interfaces;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class OutboxReceivedItem<TUniqueIdentifier, TPayload> : ISqlTransactionalOutboxPublishedItem<TUniqueIdentifier, TPayload>
    {
        public bool IsStatusFinalized { get; protected set; } = false;
        public OutboxReceivedItemProcessingStatus Status { get; protected set;  } = OutboxReceivedItemProcessingStatus.RejectAndAbandon;
        public ISqlTransactionalOutboxItem<TUniqueIdentifier> PublishedItem { get; }

        protected ILookup<string, string> HeadersLookup = null;
        protected bool IsDisposed { get; set; } = false;
        protected Func<ISqlTransactionalOutboxItem<TUniqueIdentifier>, TPayload> ParsePayloadFunc { get; }
        protected Func<Task> AcknowledgeReceiptAsyncFunc { get; }
        protected Func<Task> RejectAndAbandonReceiptAsyncFunc { get; }
        protected Func<Task> RejectAsDeadLetterReceiptAsyncFunc { get; }

        public bool IsFifoEnforcedReceivingEnabled { get; }
        public string FifoGroupingIdentifier { get; }

        public OutboxReceivedItem(
            ISqlTransactionalOutboxItem<TUniqueIdentifier> receivedOutboxItem,
            ILookup<string, string> headersLookup,
            Func<Task> acknowledgeReceiptAsyncFunc,
            Func<Task> rejectAbandonReceiptAsyncFunc,
            Func<Task> rejectDeadLetterReceiptAsyncFunc,
            Func<ISqlTransactionalOutboxItem<TUniqueIdentifier>, TPayload> parsePayloadFunc,
            bool enableFifoEnforcedReceiving,
            string fifoGroupingIdentifier
        )
        {
            IsFifoEnforcedReceivingEnabled = enableFifoEnforcedReceiving;
            FifoGroupingIdentifier = fifoGroupingIdentifier;
            PublishedItem = receivedOutboxItem.AssertNotNull(nameof(receivedOutboxItem));
            HeadersLookup = headersLookup.AssertNotNull(nameof(headersLookup));

            AcknowledgeReceiptAsyncFunc = acknowledgeReceiptAsyncFunc.AssertNotNull(nameof(acknowledgeReceiptAsyncFunc));
            RejectAndAbandonReceiptAsyncFunc = rejectAbandonReceiptAsyncFunc.AssertNotNull(nameof(rejectAbandonReceiptAsyncFunc));
            RejectAsDeadLetterReceiptAsyncFunc = rejectDeadLetterReceiptAsyncFunc.AssertNotNull(nameof(rejectDeadLetterReceiptAsyncFunc));
            ParsePayloadFunc = parsePayloadFunc.AssertNotNull(nameof(parsePayloadFunc));
        }

        public TPayload GetPayload()
        {
            var payload = ParsePayloadFunc(PublishedItem);
            return payload;
        }

        public string GetHeader(string headerKey, string defaultValue = null)
        {
            return HeadersLookup[headerKey].FirstOrDefault() ?? defaultValue;
        }

        public virtual async Task AcknowledgeSuccessfulReceiptAsync()
        {
            //Ensure that we are re-entrant and don't attempt to finalize again...
            if (IsStatusFinalized)
                return;

            this.Status = OutboxReceivedItemProcessingStatus.AcknowledgeSuccessfulReceipt;
            await AcknowledgeReceiptAsyncFunc().ConfigureAwait(false);
            IsStatusFinalized = true;
        }

        public virtual async Task RejectAndAbandonAsync()
        {
            //Ensure that we are re-entrant and don't attempt to finalize again...
            if (IsStatusFinalized)
                return;

            this.Status = OutboxReceivedItemProcessingStatus.RejectAndAbandon;
            await RejectAndAbandonReceiptAsyncFunc().ConfigureAwait(false);
            IsStatusFinalized = true;
        }

        public virtual async Task RejectAsDeadLetterAsync()
        {
            //Ensure that we are re-entrant and don't attempt to finalize again...
            if (IsStatusFinalized)
                return;

            this.Status = OutboxReceivedItemProcessingStatus.RejectAsDeadLetter;
            await RejectAsDeadLetterReceiptAsyncFunc().ConfigureAwait(false);
            IsStatusFinalized = true;
        }
    }
}
