using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutboxHelpers
{
    public interface ISqlTransactionalOutboxUniqueIdFactory<out TUniqueIdentifier>
    {
        TUniqueIdentifier CreateUniqueIdentifier();
    }
}
