using SqlTransactionalOutbox.Caching;
using System;
using SystemTextJsonHelpers;

namespace SqlTransactionalOutbox
{
    public class OutboxPayloadJsonSerializer : ISqlTransactionalOutboxSerializer
    {
        public string SerializePayload<TPayload>(TPayload payload)
        {
            return payload switch
            {
                string stringPayload => stringPayload,
                //Use Json as Default Serialization for the vast majority (if not all) use cases...
                _ => payload.ToJson(SqlTransactionalOutboxDefaults.DefaultJsonSerializerOptions)
            };
        }

        public TPayload DeserializePayload<TPayload>(string payload)
        {
            var payloadType = typeof(TPayload);
            if (payloadType == TypeCache.String || payloadType.IsAssignableFrom(TypeCache.String))
                return (TPayload)(object)payload;
            else
            {
                //Use Json as Default Serialization for the vast majority (if not all) use cases...
                var deserializedResult = payload.FromJsonTo<TPayload>(SqlTransactionalOutboxDefaults.DefaultJsonSerializerOptions);
                return deserializedResult;
            }
        }

    }
}
