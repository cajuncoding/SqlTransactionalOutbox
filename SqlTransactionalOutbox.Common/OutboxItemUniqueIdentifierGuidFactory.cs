using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutbox
{
    public class OutboxItemUniqueIdentifierGuidFactory : ISqlTransactionalOutboxUniqueIdFactory<Guid>
    {
        public Guid CreateUniqueIdentifier()
        {
            var uniqueId = Guid.NewGuid();//.ToString("B");
            return uniqueId;
        }
    }
}
