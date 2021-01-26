using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SqlTransactionalOutbox.Interfaces
{
    public interface ISqlTransactionalOutboxPublishedItem<TUniqueIdentifier, out TPayload>
    {
        bool IsStatusFinalized { get; }
        
        OutboxReceivedItemProcessingStatus Status { get; }

        ISqlTransactionalOutboxItem<TUniqueIdentifier> PublishedItem { get; }
        
        public bool IsFifoEnforcedReceivingEnabled { get; }
        
        public string FifoGroupingIdentifier { get; }

        TPayload GetPayload();

        Task AcknowledgeSuccessfulReceiptAsync();

        Task RejectAndAbandonAsync();

        Task RejectAsDeadLetterAsync();
    }
}
