using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SqlTransactionalOutbox
{
    public interface ISqlTransactionalOutboxSerializer
    {
        string SerializePayload<TPayload>(TPayload payload);
        TPayload DeserializePayload<TPayload>(string serializedPayload);
    }
}
