using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SqlTransactionalOutbox
{
    public interface ISqlTransactionalOutbox<TUniqueIdentifier, TPayload>
    {
        Task<ISqlTransactionalOutboxItem<TUniqueIdentifier>> InsertNewPendingOutboxItemAsync(
            string publishingTarget, 
            TPayload publishingPayload
        );

        Task<List<ISqlTransactionalOutboxItem<TUniqueIdentifier>>> InsertNewPendingOutboxItemsAsync(
            IEnumerable<ISqlTransactionalOutboxInsertionItem<TPayload>> outboxInsertionItems
        );
    }
}
