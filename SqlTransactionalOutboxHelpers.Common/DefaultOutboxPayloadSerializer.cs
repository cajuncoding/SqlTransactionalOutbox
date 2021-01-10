using System;
using Newtonsoft.Json;

namespace SqlTransactionalOutboxHelpers
{
    public class DefaultOutboxPayloadSerializer : ISqlTransactionalOutboxSerializer
    {
        public string SerializePayload<TPayload>(TPayload payload)
        {
            //Use Json as Default Serialization for the vast majority (if not all) use cases...
            var serializedResult = JsonConvert.SerializeObject(payload);
            return serializedResult;
        }
    }
}
