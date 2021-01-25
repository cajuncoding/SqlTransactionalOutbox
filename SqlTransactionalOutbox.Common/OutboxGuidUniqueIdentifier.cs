using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutbox
{
    public class OutboxGuidUniqueIdentifier : ISqlTransactionalOutboxUniqueIdFactory<Guid>
    {
        public Guid CreateUniqueIdentifier()
        {
            var uniqueId = Guid.NewGuid();//.ToString("B");
            return uniqueId;
        }

        public Guid ParseUniqueIdentifier(string uniqueIdentifier)
        {
            return Guid.Parse(uniqueIdentifier);
        }
    }
}
