using System;
using SqlTransactionalOutbox.Utilities;
using SystemTextJsonHelpers;

namespace SqlTransactionalOutbox
{
    public class OutboxPayloadJsonSerializer : ISqlTransactionalOutboxSerializer
    {
        private static readonly Type _stringTypeCache = typeof(string);

        public string SerializePayload<TPayload>(TPayload payload)
        {
            return payload switch
            {
                string stringPayload => stringPayload,
                //Use Json as Default Serialization for the vast majority (if not all) use cases...
                _ => payload.ToJson(PayloadBuilder.OutboxJsonSerializerOptions)
            };
        }

        public TPayload DeserializePayload<TPayload>(string payload)
        {
            var payloadType = typeof(TPayload);
            if (payloadType == _stringTypeCache || payloadType.IsAssignableFrom(_stringTypeCache))
                return (TPayload)(object)payload;
            else
            {
                //Use Json as Default Serialization for the vast majority (if not all) use cases...
                var deserializedResult = payload.FromJsonTo<TPayload>(PayloadBuilder.OutboxJsonSerializerOptions);
                return deserializedResult;
            }
        }

    }
}
