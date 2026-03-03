using System.Threading;
using System.Threading.Tasks;
using SqlTransactionalOutbox.Receiving;

namespace SqlTransactionalOutbox
{
    public interface ISqlTransactionalOutboxReceivedItem<TUniqueIdentifier, out TPayloadBody>
    {
        OutboxReceivedItemProcessingStatus Status { get; }

        ISqlTransactionalOutboxItem<TUniqueIdentifier> PublishedItem { get; }

        TUniqueIdentifier UniqueIdentifier { get; }

        string ContentType { get; }

        string CorrelationId { get; }

        bool IsStatusFinalized { get; }
        
        public bool IsFifoEnforcedReceivingEnabled { get; }
        
        public string FifoGroupingIdentifier { get; }

        string Subject { get; }

        public TPayloadBody ParsedBody { get; }

        TPayloadBody ParsePayloadBody();

        Task AcknowledgeSuccessfulReceiptAsync(CancellationToken cancellationToken = default);

        Task RejectAndAbandonAsync(CancellationToken cancellationToken = default);

        Task RejectAsDeadLetterAsync(CancellationToken cancellationToken = default);
    }
}
