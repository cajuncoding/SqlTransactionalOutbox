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
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            // Ensure DateTimeOffset is used when reading
            DateParseHandling = DateParseHandling.DateTimeOffset,
            // Preserve whatever offset is in the JSON (Z, +00:00, -06:00, etc.)
            DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind
        };

        public static JsonSerializer OutboxJsonSerializer { get; set; } = JsonSerializer.Create(OutboxJsonSerializerSettings);

        public string PublishTarget { get; set; }
        public string To { get; set; }
        public string FifoGroupingId { get; set; }
        public DateTimeOffset? ScheduledPublishDateTimeUtc { get; set; }
        public TimeSpan? ScheduledDelayTimeSpan { get; set; }
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
             
            var json = JsonHelpers.ParseSafely(jsonText, OutboxJsonSerializerSettings);
            if (json != null)
                payload.ApplyValues(json);

            return payload;
        }

        public PayloadBuilder ApplyValues<TModel>(TModel model, bool overwriteExisting = true)
        {
            model.AssertNotNull(nameof(model));
            
            var json = JObject.FromObject(model, OutboxJsonSerializer);
            ApplyValues(json, overwriteExisting);

            return this;
        }

        public PayloadBuilder ApplyValues(JObject json, bool overwriteExisting = true)
        {
            json.AssertNotNull(nameof(json));

            var jsonValuesCaseInsensitiveLookup = json.Properties().ToLookup(
                p => p.Name,
                p => p.Value.Type switch
                {
                    JTokenType.Null => null,
                    //Use ISO 8601 format for DateTime values to ensure consistency and proper parsing on the receiving end regardless of culture/locale settings.
                    JTokenType.Date => json.ValueSafely<DateTimeOffset?>(p.Name)?.ToIso8601RoundTripFormat()
                                        ?? json.ValueSafely<DateTime?>(p.Name)?.ToIso8601RoundTripFormat(),
                    JTokenType.Boolean => p.Value.Value<bool>().ToString(),
                    _ => p.Value.ToString()
                },
                StringComparer.OrdinalIgnoreCase
            );

            //Dynamically detect if the Json has a Body property (case-insensitive) and if not, then we will use the entire Json as the default Body content.
            //  This allows for maximum flexibility in how the JSON Payload can be structured allowing dynamic Message Property initialization (e.g. PublishTarget, To, Subject, etc.) 
            //  without forcing a specific structure on the JSON Payload...
            //NOTE: This is necessary for the downstream ApplyValues() to correctly initialize the Body value from the JSON.
            var jsonBodyProperty = json.Property(JsonMessageFields.Body, StringComparison.OrdinalIgnoreCase);
            if (jsonBodyProperty == null)
                json[JsonMessageFields.Body] = json.ToString(Formatting.None);

            //Apply all discrete values...
            ApplyValues(jsonValuesCaseInsensitiveLookup, overwriteExisting);

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

        public PayloadBuilder ApplyValues(ILookup<string, string> caseInsensitiveValuesLookup, bool overwriteExisting = true)
        {
            caseInsensitiveValuesLookup.AssertNotNull(nameof(caseInsensitiveValuesLookup));

            PublishTarget = InitStringValue(PublishTarget, caseInsensitiveValuesLookup, overwriteExisting,
                JsonMessageFields.PublishTarget,
                JsonMessageFields.PublishTopic,
                JsonMessageFields.QueueTarget,
                JsonMessageFields.Topic
            );
            
            To = InitStringValue(To, caseInsensitiveValuesLookup, overwriteExisting, JsonMessageFields.To);
            
            FifoGroupingId = InitStringValue(FifoGroupingId, caseInsensitiveValuesLookup, overwriteExisting,
                JsonMessageFields.FifoGroupingId,
                JsonMessageFields.SessionId
            );

            //Process teh Scheduled Publish Time as strongly typed DateTimeOffset...
            if (DateTimeOffset.TryParse(
                    InitStringValue(ScheduledPublishDateTimeUtc?.ToString(), caseInsensitiveValuesLookup, overwriteExisting, 
                        JsonMessageFields.ScheduledPublishDateTimeUtc,
                        JsonMessageFields.ScheduledPublishTime
                    ),
                    out var scheduleDateTime
                )
            )
            {
                ScheduledPublishDateTimeUtc = scheduleDateTime;
                //Since an absolute Schedule Publish DateTime was specified we derive the actual Schedule Delay and populate it
                //NOTE: This is helpful for reference, support, logging, etc.
                ScheduledDelayTimeSpan = ScheduledPublishDateTimeUtc.Value - DateTimeOffset.UtcNow;
            }

            //OPTIONALLY, for convenience, handle Schedule Delay if an absolute Schedule time is not present...
            if(ScheduledPublishDateTimeUtc == null)
            {
                var scheduleDelayTimeTemplate = InitStringValue(ScheduledDelayTimeSpan?.ToString(), caseInsensitiveValuesLookup, overwriteExisting,
                    JsonMessageFields.ScheduledPublishDelay,
                    JsonMessageFields.ScheduledPublishDelayTimeSpan,
                    JsonMessageFields.ScheduledPublishDelayTemplate
                );

                if (scheduleDelayTimeTemplate.TryParseTimeSpanWithUnitsAndMinutesDefault(out var timeSpan))
                {
                    ScheduledDelayTimeSpan = timeSpan;
                    //Now we initialize the Absolute Schedule Date using the specified delay for all downstream code to use!
                    ScheduledPublishDateTimeUtc = DateTimeOffset.UtcNow.Add(timeSpan);
                }
            }

            Subject = InitStringValue(Subject, caseInsensitiveValuesLookup, overwriteExisting, JsonMessageFields.Subject, JsonMessageFields.Label);
            
            CorrelationId = InitStringValue(CorrelationId, caseInsensitiveValuesLookup, overwriteExisting, JsonMessageFields.CorrelationId);
            
            ReplyTo = InitStringValue(ReplyTo, caseInsensitiveValuesLookup, overwriteExisting, JsonMessageFields.ReplyTo);
            
            ReplyToSessionId = InitStringValue(ReplyToSessionId, caseInsensitiveValuesLookup, overwriteExisting, JsonMessageFields.ReplyToSessionId);

            //Init the Body and fallback to the original Json if not specified explicitly...
            Body = InitStringValue(Body, caseInsensitiveValuesLookup, overwriteExisting, JsonMessageFields.Body);

            //Look for ContentType but then fallback to detect it if not specified explicitly...
            ContentType = InitStringValue(ContentType, caseInsensitiveValuesLookup, overwriteExisting, JsonMessageFields.ContentType)
                ?? DetectContentType(Body);

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

        protected virtual string InitStringValue(string currentValue, ILookup<string, string> jsonValuesLookup, bool overwriteExisting, params string[] fieldNames)
        {
            string lookupValue = fieldNames?
                //Map all specified names to lookup values (or null)
                //NOTE: The Lookup should be Case Insenstivie already...
                .Select(n => jsonValuesLookup[n].FirstOrDefault())
                //Find the First non-null/empty/whitespace value...
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

            if (lookupValue != null && (overwriteExisting || string.IsNullOrWhiteSpace(currentValue)))
            {
                //If current value is null/empty/whitespace or we can overwrite (not apply as fallback) then return the lookup value that we have!
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
