using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SqlTransactionalOutbox
{
    public interface ISqlTransactionalOutboxPublisher<TUniqueIdentifier>
    {
        Task PublishOutboxItemAsync(
            ISqlTransactionalOutboxItem<TUniqueIdentifier> outboxItem, 
            bool isFifoEnforcedProcessingEnabled = false
        );

    }
}
