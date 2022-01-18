using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.JsonExtensions;
using SqlTransactionalOutbox.Publishing;

namespace SqlTransactionalOutbox.Utilities
{
    public class PayloadBuilder
    {
        public static JsonSerializerSettings OutboxJsonSerializerSettings { get; set; } = new JsonSerializerSettings()
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            },
            NullValueHandling = NullValueHandling.Ignore
        };

        public static JsonSerializer OutboxJsonSerializer { get; set; } = JsonSerializer.Create(OutboxJsonSerializerSettings);

        public string PublishTarget { get; set; }
        public string To { get; set; }
        public string FifoGroupingId { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public string ContentType { get; set; }
        public string CorrelationId { get; set; }
        public string ReplyTo { get; set; }
        public string ReplyToSessionId { get; set; }
        public Dictionary<string, string> Headers { get; set; }

        public static PayloadBuilder FromObject<TObject>(TObject obj)
        {
            var payload = new PayloadBuilder();

            var json = JObject.FromObject(obj, OutboxJsonSerializer);
            payload.ApplyValues(json);

            return payload;
        }

        public static PayloadBuilder FromJsonSafely(string jsonText)
        {
            var payload = new PayloadBuilder();

            var json = JsonHelpers.ParseSafely(jsonText);
            if (json != null)
                payload.ApplyValues(json);

            return payload;
        }

        public PayloadBuilder ApplyValues(JObject json, bool overwriteExisting = true)
        {
            json.AssertNotNull(nameof(json));

            var jsonLookup = json.Properties().ToLookup(
                p => p.Name.ToLower(),
                p => p.Value.ToString()
            );

            //Apply all discrete values...
            ApplyValues(jsonLookup, overwriteExisting);

            //Headers are available via Json Payload as a nested Json object (Key/Value)...
            var jsonHeaders = json.ValueSafely<JObject>(JsonMessageFields.Headers)
                                        ?? json.ValueSafely<JObject>(JsonMessageFields.UserProperties);
            if (jsonHeaders != null)
            {
                this.Headers = new Dictionary<string, string>();
                //NOTE: We DO NOT convert headers to encoded names here for simplicity and to
                //      prevent duplicate header name construction/encoding.
                foreach (var prop in jsonHeaders.Properties())
                {
                    this.Headers[prop.Name] = prop.Value.ToString();
                }
            }

            //Make Chainable...
            return this;
        }

        public PayloadBuilder ApplyValues(ILookup<string, string> lookup, bool overwriteExisting = true)
        {
            lookup.AssertNotNull(nameof(lookup));

            PublishTarget = InitValue(PublishTarget, lookup, overwriteExisting,
                JsonMessageFields.PublishTarget, JsonMessageFields.PublishTopic, 
                                JsonMessageFields.QueueTarget, JsonMessageFields.Topic);
            
            To = InitValue(To, lookup, overwriteExisting, JsonMessageFields.To);
            
            FifoGroupingId = InitValue(FifoGroupingId, lookup, overwriteExisting, 
                JsonMessageFields.FifoGroupingId, JsonMessageFields.SessionId);
            
            Subject = InitValue(Subject, lookup, overwriteExisting, 
                JsonMessageFields.Subject, JsonMessageFields.Label);
            
            CorrelationId = InitValue(CorrelationId, lookup, overwriteExisting, 
                JsonMessageFields.CorrelationId);
            
            ReplyTo = InitValue(ReplyTo, lookup, overwriteExisting, 
                JsonMessageFields.ReplyTo);
            
            ReplyToSessionId = InitValue(ReplyToSessionId, lookup, overwriteExisting, 
                JsonMessageFields.ReplyToSessionId);

            //Init the Body and fallback to the original Json is not specified explicitly...
            Body = InitValue(Body, lookup, overwriteExisting, 
                JsonMessageFields.Body) ?? lookup.ToString();

            //Look for ContentType but then fallback to detect it if not specified explicitly...
            ContentType = InitValue(ContentType, lookup, overwriteExisting, 
                JsonMessageFields.ContentType) ?? DetectContentType(Body);

            ////Headers must be defined by JSON Payload for now...
            //var headersList = new List<KeyValuePair<string, string>>();

            //if (headersList?.Any() == true)
            //    Headers = headersList;

            //Make Chainable...
            return this;
        }

        /// <summary>
        /// Build a valid JObject from this Payload with standard default properties...
        /// </summary>
        /// <returns></returns>
        public JObject ToJObject()
        {
            var json = JObject.FromObject(this, OutboxJsonSerializer);
            return json;
        }

        protected virtual string InitValue(string currentValue, ILookup<string, string> lookup, bool overwriteExisting, params string[] fieldNames)
        {
            string lookupValue = fieldNames?
                //Map all specified names to lookup values (or null)
                .Select(n => lookup[n.ToLower()].FirstOrDefault())
                //Find the First non-null value...
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

            if (!string.IsNullOrWhiteSpace(lookupValue) && (overwriteExisting || string.IsNullOrWhiteSpace(currentValue)))
            {
                //If current value is null or we can overwrite (not apply as fallback) then return the lookup value that we have!
                return lookupValue;
            }

            return currentValue;
        }

        protected virtual string DetectContentType(string payloadBody)
        {
            var isValidJson = JsonHelpers.IsValidJson(payloadBody);
            var contentType = isValidJson ? MessageContentTypes.Json : MessageContentTypes.PlainText;
            return contentType;
        }
    }
}
