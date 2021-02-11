using System;
using System.Collections.Generic;
using System.Text;
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

        TPayloadBody ParsePayloadBody();

        Task AcknowledgeSuccessfulReceiptAsync();

        Task RejectAndAbandonAsync();

        Task RejectAsDeadLetterAsync();
    }
}
