using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox.Receiving
{
    public class OutboxReceivedItem<TUniqueIdentifier, TPayload> : ISqlTransactionalOutboxReceivedItem<TUniqueIdentifier, TPayload>
    {
        public bool IsStatusFinalized { get; protected set; } = false;
        public OutboxReceivedItemProcessingStatus Status { get; protected set;  } = OutboxReceivedItemProcessingStatus.RejectAndAbandon;
        public ISqlTransactionalOutboxItem<TUniqueIdentifier> PublishedItem { get; protected set; }
        public TUniqueIdentifier UniqueIdentifier { get; protected set; }
        public string ContentType { get; protected set; }
        public string CorrelationId { get; protected set; }


        protected ILookup<string, object> HeadersLookup = null;
        protected bool IsDisposed { get; set; } = false;
        protected Func<ISqlTransactionalOutboxItem<TUniqueIdentifier>, TPayload> ParsePayloadFunc { get; set; }
        
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
            bool enableFifoEnforcedReceiving = false,
            string fifoGroupingIdentifier = null,
            string correlationId = null
        )
        {
            InitBaseOutboxReceivedItem(
                outboxItem,
                headersLookup,
                contentType,
                enableFifoEnforcedReceiving,
                fifoGroupingIdentifier,
                correlationId
            );
        }

        protected void InitBaseOutboxReceivedItem(
            ISqlTransactionalOutboxItem<TUniqueIdentifier> outboxItem,
            ILookup<string, object> headersLookup,
            string contentType,
            bool isFifoProcessingEnabled = false,
            string fifoGroupingIdentifier = null,
            string correlationId = null
        )
        {
            PublishedItem = outboxItem.AssertNotNull(nameof(outboxItem));
            HeadersLookup = headersLookup.AssertNotNull(nameof(headersLookup));

            UniqueIdentifier = outboxItem.UniqueIdentifier;
            ContentType = string.IsNullOrWhiteSpace(contentType) ? MessageContentTypes.PlainText : contentType;

            CorrelationId = correlationId;
            IsFifoEnforcedReceivingEnabled = isFifoProcessingEnabled;
            FifoGroupingIdentifier = fifoGroupingIdentifier;
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
            IsStatusFinalized = true;
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
