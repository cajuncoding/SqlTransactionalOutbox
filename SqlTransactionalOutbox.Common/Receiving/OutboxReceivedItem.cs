using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox
{
    public class OutboxReceivedItem<TUniqueIdentifier, TPayload> : ISqlTransactionalOutboxReceivedItem<TUniqueIdentifier, TPayload>
    {
        public string ContentType { get; protected set; }
        public string CorrelationId { get; protected set; }

        public bool IsStatusFinalized { get; protected set; } = false;
        public OutboxReceivedItemProcessingStatus Status { get; protected set;  } = OutboxReceivedItemProcessingStatus.RejectAndAbandon;
        public ISqlTransactionalOutboxItem<TUniqueIdentifier> PublishedItem { get; }

        protected ILookup<string, object> HeadersLookup = null;
        protected bool IsDisposed { get; set; } = false;
        protected Func<ISqlTransactionalOutboxItem<TUniqueIdentifier>, TPayload> ParsePayloadFunc { get; }
        protected Func<Task> AcknowledgeReceiptAsyncFunc { get; }
        protected Func<Task> RejectAndAbandonReceiptAsyncFunc { get; }
        protected Func<Task> RejectAsDeadLetterReceiptAsyncFunc { get; }

        public bool IsFifoEnforcedReceivingEnabled { get; }
        public string FifoGroupingIdentifier { get; }

        public OutboxReceivedItem(
            ISqlTransactionalOutboxItem<TUniqueIdentifier> outboxItem,
            ILookup<string, object> headersLookup,
            string contentType,
            Func<Task> acknowledgeReceiptAsyncFunc,
            Func<Task> rejectAbandonReceiptAsyncFunc,
            Func<Task> rejectDeadLetterReceiptAsyncFunc,
            Func<ISqlTransactionalOutboxItem<TUniqueIdentifier>, TPayload> parsePayloadFunc,
            bool enableFifoEnforcedReceiving = false,
            string fifoGroupingIdentifier = null,
            string correlationId = null
            )
        {
            CorrelationId = correlationId;
            IsFifoEnforcedReceivingEnabled = enableFifoEnforcedReceiving;
            FifoGroupingIdentifier = fifoGroupingIdentifier;
            
            ContentType = string.IsNullOrWhiteSpace(contentType) ? MessageContentTypes.PlainText : contentType;
            PublishedItem = outboxItem.AssertNotNull(nameof(outboxItem));
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

        public T GetHeaderValue<T>(string headerKey, T defaultValue = default)
        {
            return (T)HeadersLookup[headerKey].FirstOrDefault() ?? defaultValue;
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
