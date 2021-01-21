using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutbox
{
    public interface ISqlTransactionalOutboxUniqueIdFactory<out TUniqueIdentifier>
    {
        TUniqueIdentifier CreateUniqueIdentifier();
    }
}
