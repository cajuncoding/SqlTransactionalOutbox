using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlTransactionalOutbox.Utilities;

namespace SqlTransactionalOutbox
{
    public class OutboxPayloadJsonSerializer : ISqlTransactionalOutboxSerializer
    {
        private static readonly Type _stringType = typeof(string);
        private static readonly Type _jTokenType = typeof(JToken);
        //private static readonly Type _jObjectType = typeof(JObject);

        public string SerializePayload<TPayload>(TPayload payload)
        {
            switch (payload)
            {
                case string stringPayload:
                    return stringPayload;
                //BBernard - 01/18/2022
                //NOTE: TO ensure that our JsonSerializer Settings are applies we can't use ToString(), but
                //      normal JsonConvert.SerializeObject() handles the JToken just fine!
                //case JToken jsonPayload:
                //    return jsonPayload.ToString();
                default:
                {
                    //Use Json as Default Serialization for the vast majority (if not all) use cases...
                    var serializedResult = JsonConvert.SerializeObject(payload, PayloadBuilder.OutboxJsonSerializerSettings);
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
                var deserializedResult = JsonConvert.DeserializeObject<TPayload>(payload, PayloadBuilder.OutboxJsonSerializerSettings);
                return deserializedResult;
            }
        }

    }
}
