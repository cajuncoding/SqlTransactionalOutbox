using System;
using System.Threading.Tasks;

namespace SqlTransactionalOutbox.Receiving
{
    public interface ISqlTransactionalOutboxReceivedItemHandler<TUniqueIdentifier, in TPayload>
    {
        Task HandleReceivedItemAsync(ISqlTransactionalOutboxReceivedItem<TUniqueIdentifier, TPayload> outboxReceivedItem);
    }
}
