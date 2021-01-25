using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SqlTransactionalOutbox.Interfaces
{
    public interface ISqlTransactionalOutboxReceivedMessage<TUniqueIdentifier, out TPayload>
    {
        ISqlTransactionalOutboxItem<TUniqueIdentifier> ReceivedItem { get; }

        TPayload GetPayload();
    }
}
