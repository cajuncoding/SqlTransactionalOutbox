using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SqlTransactionalOutbox
{
    public class OutboxPayloadJsonSerializer : ISqlTransactionalOutboxSerializer
    {
        public string SerializePayload<TPayload>(TPayload payload)
        {
            switch (payload)
            {
                case string stringPayload:
                    return stringPayload;
                case JObject jsonObjectPayload:
                    return jsonObjectPayload.ToString();
                default:
                {
                    //Use Json as Default Serialization for the vast majority (if not all) use cases...
                    var serializedResult = JsonConvert.SerializeObject(payload);
                    return serializedResult;
                }
            }
        }

        public TPayload DeserializePayload<TPayload>(string payload)
        {
            var payloadType = typeof(TPayload);
            if (payloadType == typeof(string))
            {
                return (TPayload)(object)payload;
            }
            else if (payloadType == typeof(JObject))
            {
                return (TPayload)(object)JObject.Parse(payload);
            }
            else
            {
                //Use Json as Default Serialization for the vast majority (if not all) use cases...
                var deserializedResult = JsonConvert.DeserializeObject<TPayload>(payload);
                return deserializedResult;
            }
        }

    }
}
