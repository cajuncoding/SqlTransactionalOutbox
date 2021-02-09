using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SqlTransactionalOutbox
{
    public class OutboxPayloadJsonSerializer : ISqlTransactionalOutboxSerializer
    {
        private static readonly Type _stringType = typeof(string);
        private static readonly Type _jTokenType = typeof(JToken);
        //private static readonly Type _jObjecgtType = typeof(JObject);

        public string SerializePayload<TPayload>(TPayload payload)
        {
            switch (payload)
            {
                case string stringPayload:
                    return stringPayload;
                case JToken jsonPayload:
                    return jsonPayload.ToString();
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
            if (payloadType == _stringType || payloadType.IsAssignableFrom(_stringType))
            {
                return (TPayload)(object)payload;
            }
            else if (_jTokenType.IsAssignableFrom(payloadType))
            {
                return (TPayload)(object)JToken.Parse(payload);
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
