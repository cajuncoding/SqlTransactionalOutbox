using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutboxHelpers
{
    public interface ISqlTransactionalOutboxItemFactory
    {
        ISqlTransactionalOutboxItem CreateOutboxItem(string publishingTarget, string publishingPayload, TimeSpan? timeSpanToLive = null);
        
        string CreateUniqueIdentifier();
    }
}
