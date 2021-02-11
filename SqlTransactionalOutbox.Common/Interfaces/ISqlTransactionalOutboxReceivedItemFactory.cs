using System;

namespace SqlTransactionalOutbox
{
    public interface ISqlTransactionalOutboxReceivedItemFactory<TUniqueIdentifier, TPayload>
    {
        ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> OutboxItemFactory { get; }
        ISqlTransactionalOutboxReceivedItem<TUniqueIdentifier, TPayload> CreateReceivedOutboxItem();
    }
}