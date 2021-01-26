using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SqlTransactionalOutbox.Interfaces;

namespace SqlTransactionalOutbox.Receiving
{
    public interface ISqlTransactionalOutboxReceivedItemHandler<TUniqueIdentifier, in TPayload>
    {
        Task HandleReceivedItemAsync(
            ISqlTransactionalOutboxPublishedItem<TUniqueIdentifier, TPayload> outboxPublishedItem
        );
    }
}
