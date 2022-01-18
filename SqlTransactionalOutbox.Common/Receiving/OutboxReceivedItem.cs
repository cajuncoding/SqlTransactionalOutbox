using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox.Receiving
{
    public class OutboxReceivedItem<TUniqueIdentifier, TPayloadBody> : ISqlTransactionalOutboxReceivedItem<TUniqueIdentifier, TPayloadBody>
    {
        public bool IsStatusFinalized { get; protected set; } = false;
        public OutboxReceivedItemProcessingStatus Status { get; protected set;  } = OutboxReceivedItemProcessingStatus.RejectAndAbandon;
        public ISqlTransactionalOutboxItem<TUniqueIdentifier> PublishedItem { get; protected set; }
        public TUniqueIdentifier UniqueIdentifier { get; protected set; }
        public string ContentType { get; protected set; }
        public string Subject { get; protected set; }
        public string PayloadSerializedBody => PublishedItem?.Payload;
        public string CorrelationId { get; protected set; }

        private TPayloadBody _parsedBody;

        public TPayloadBody ParsedBody => _parsedBody ??= ParsePayloadBody();
        
        protected ILookup<string, object> HeadersLookup = null;
        protected bool IsDisposed { get; set; } = false;
        protected Func<ISqlTransactionalOutboxItem<TUniqueIdentifier>, TPayloadBody> ParsePayloadFunc { get; set; }
        
        public bool IsFifoEnforcedReceivingEnabled { get; protected set; }
        public string FifoGroupingIdentifier { get; protected set; }

        protected OutboxReceivedItem()
        {
            //Empty Constructor for inheriting /implementing classes.
        }

        public OutboxReceivedItem(
            ISqlTransactionalOutboxItem<TUniqueIdentifier> outboxItem,
            ILookup<string, object> headersLookup,
            string contentType,
            Func<ISqlTransactionalOutboxItem<TUniqueIdentifier>, TPayloadBody> parsePayloadFunc,
            string subject = null,
            bool enableFifoEnforcedReceiving = false,
            string fifoGroupingIdentifier = null,
            string correlationId = null
        )
        {
            InitBaseOutboxReceivedItem(
                outboxItem,
                headersLookup,
                contentType,
                parsePayloadFunc,
                isFifoProcessingEnabled: enableFifoEnforcedReceiving,
                subject: subject,
                fifoGroupingIdentifier: fifoGroupingIdentifier,
                correlationId: correlationId
            );
        }

        protected void InitBaseOutboxReceivedItem(
            ISqlTransactionalOutboxItem<TUniqueIdentifier> outboxItem,
            ILookup<string, object> headersLookup,
            string contentType,
            Func<ISqlTransactionalOutboxItem<TUniqueIdentifier>, TPayloadBody> parsePayloadFunc,
            bool isFifoProcessingEnabled,
            string subject = null,
            string fifoGroupingIdentifier = null,
            string correlationId = null
        )
        {
            Subject = subject;// Optional; Null if not specified or not supported.

            PublishedItem = outboxItem.AssertNotNull(nameof(outboxItem));
            HeadersLookup = headersLookup.AssertNotNull(nameof(headersLookup));

            UniqueIdentifier = outboxItem.UniqueIdentifier;
            ContentType = string.IsNullOrWhiteSpace(contentType) ? MessageContentTypes.PlainText : contentType;
            ParsePayloadFunc = parsePayloadFunc.AssertNotNull(nameof(parsePayloadFunc));

            CorrelationId = correlationId;
            IsFifoEnforcedReceivingEnabled = isFifoProcessingEnabled;
            FifoGroupingIdentifier = fifoGroupingIdentifier;
        }

        public TPayloadBody ParsePayloadBody()
        {
            var payload = ParsePayloadFunc(PublishedItem);
            return payload;
        }

        public string GetPayloadSerializedBody()
        {
            return this.PayloadSerializedBody;
        }

        public T GetHeaderValue<T>(string headerKey, T defaultValue = default)
        {
            return (T)HeadersLookup[headerKey].FirstOrDefault() ?? defaultValue;
        }

        public virtual Task AcknowledgeSuccessfulReceiptAsync()
        {
            //Ensure that we are re-entrant and don't attempt to finalize again...
            if (!IsStatusFinalized)
            {
                this.Status = OutboxReceivedItemProcessingStatus.AcknowledgeSuccessfulReceipt;
                IsStatusFinalized = true;
            }

            return Task.CompletedTask;
        }

        public virtual Task RejectAndAbandonAsync()
        {
            //Ensure that we are re-entrant and don't attempt to finalize again...
            if (!IsStatusFinalized)
            {
                this.Status = OutboxReceivedItemProcessingStatus.RejectAndAbandon;
                IsStatusFinalized = true;
            }
            
            return Task.CompletedTask;
        }

        public virtual Task RejectAsDeadLetterAsync()
        {
            //Ensure that we are re-entrant and don't attempt to finalize again...
            if (!IsStatusFinalized)
            {
                this.Status = OutboxReceivedItemProcessingStatus.RejectAsDeadLetter;
                IsStatusFinalized = true;
            }

            return Task.CompletedTask;
        }
    }
}
