using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.JsonExtensions;
using SqlTransactionalOutbox.Publishing;
using SystemTextJsonHelpers;

namespace SqlTransactionalOutbox.Utilities
{
    public class PayloadBuilder
    {
        public PayloadBuilder(JsonSerializerOptions jsonSerializerOptions = null)
        {
            JsonSerializerOptions = jsonSerializerOptions ?? InternalPayloadBuilderDefaultJsonSerializerOptions;
        }

        [JsonIgnore]
        private static JsonSerializerOptions InternalPayloadBuilderDefaultJsonSerializerOptions => SqlTransactionalOutboxDefaults.DefaultJsonSerializerOptions;
        [JsonIgnore]
        public JsonSerializerOptions JsonSerializerOptions { get; set; }

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
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static PayloadBuilder FromObject<TObject>(TObject obj, JsonSerializerOptions jsonSerializerOptions = null)
        {
            var json = obj?.ToJsonNode(jsonSerializerOptions ?? InternalPayloadBuilderDefaultJsonSerializerOptions);
            return new PayloadBuilder(jsonSerializerOptions).ApplyValues(json as JsonObject);
        }

        public static PayloadBuilder FromJson(string jsonText, JsonSerializerOptions jsonSerializerOptions = null)
        {
            var json = JsonHelpers.ParseSafely(jsonText, jsonSerializerOptions ?? InternalPayloadBuilderDefaultJsonSerializerOptions);
            return new PayloadBuilder(jsonSerializerOptions).ApplyValues(json as JsonObject);
        }

        public PayloadBuilder ApplyValues<TModel>(TModel model, bool overwriteExisting = true)
        {
            var json = model?.ToJsonNode(JsonSerializerOptions);
            return ApplyValues(json as JsonObject, overwriteExisting);
        }

        public PayloadBuilder ApplyValues(JsonObject json, bool overwriteExisting = true)
        {
            if(json is null)
                return this;

            PublishTarget = InitStringValue(PublishTarget, json, overwriteExisting,
                JsonMessageFields.PublishTarget,
                JsonMessageFields.PublishTopic,
                JsonMessageFields.QueueTarget,
                JsonMessageFields.Topic
            );
            
            To = InitStringValue(To, json, overwriteExisting, JsonMessageFields.To);
            
            FifoGroupingId = InitStringValue(FifoGroupingId, json, overwriteExisting,
                JsonMessageFields.FifoGroupingId,
                JsonMessageFields.SessionId
            );

            //Process teh Scheduled Publish Time as strongly typed DateTimeOffset...
            if (DateTimeOffset.TryParse(
                    InitStringValue(ScheduledPublishDateTimeUtc?.ToString(), json, overwriteExisting, 
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
                var scheduleDelayTimeTemplate = InitStringValue(ScheduledDelayTimeSpan?.ToString(), json, overwriteExisting,
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

            Subject = InitStringValue(Subject, json, overwriteExisting, JsonMessageFields.Subject, JsonMessageFields.Label);
            
            CorrelationId = InitStringValue(CorrelationId, json, overwriteExisting, JsonMessageFields.CorrelationId);
            
            ReplyTo = InitStringValue(ReplyTo, json, overwriteExisting, JsonMessageFields.ReplyTo);
            
            ReplyToSessionId = InitStringValue(ReplyToSessionId, json, overwriteExisting, JsonMessageFields.ReplyToSessionId);

            //Init the Body and fallback to the original Json if not specified explicitly...
            //NOTE: We dynamically detect if the Payload Body can be successfully initialized (e.g. Json has a Body property (case-insensitive)) and if not,
            //  then we will use the entire Json as the default Body content. This allows for maximum flexibility in how the JSON Payload can b
            //  structured allowing dynamic Message Property initialization (e.g. PublishTarget, To, Subject, etc.) without forcing a specific
            //  structure on the JSON Payload...
            //NOTE: For the Body specifically we enable support for Complex values such as JsonObject, etc... and we co-erce it to string!
            var bodyIsMissing = string.IsNullOrWhiteSpace(Body);
            var shouldOverwriteBody =  bodyIsMissing || overwriteExisting;
            Body = json[JsonMessageFields.Body] switch
            {
                JsonObject jsonObject when shouldOverwriteBody => jsonObject.ToJsonString(JsonSerializerOptions),
                JsonNode jsonNode when shouldOverwriteBody && jsonNode.TryGetValueSafely<string>(out var value, JsonSerializerOptions) => value,
                _ when bodyIsMissing => json.ToJsonString(JsonSerializerOptions),
                //If no valid case for initializing the Body from the JSON then we fallback to the existing Body value which may be null/empty or already have a value
                _ => Body
            };

            //Look for ContentType but then fallback to detect it if not specified explicitly...
            ContentType = InitStringValue(ContentType, json, overwriteExisting, JsonMessageFields.ContentType)
                ?? DetectContentType(Body);

            //Headers are available via Json Payload as a nested Json object (Key/Value)...
            var jsonHeaders = json[JsonMessageFields.Headers]?.AsObject()
                ?? json[JsonMessageFields.UserProperties]?.AsObject();

            if (jsonHeaders != null)
            {
                this.Headers ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); ;
                //NOTE: We DO NOT convert headers to encoded names here for simplicity and to
                //      prevent duplicate header name construction/encoding.
                foreach (var prop in jsonHeaders.GetProperties(JsonDataTypeFilter.PrimitiveDataTypes))
                    this.Headers[prop.Key] = prop.Value.ValueSafely<string>(options: JsonSerializerOptions);
            }

            //Make Chainable...
            return this;
        }

        /// <summary>
        /// Build a valid JsonObject from this Payload with standard default properties...
        /// </summary>
        /// <returns></returns>
        public JsonObject ToJsonObject() => this.ToJsonNode(JsonSerializerOptions) as JsonObject;

        protected virtual string InitStringValue(string currentValue, JsonObject json, bool overwriteExisting, params string[] fieldNames)
        {
            string value = fieldNames?
                //Map all specified names to potentially existing values in our json (or null)
                //NOTE: The JsonObject should be case-insensitive as set in the relaxed JsonSerializerOptions used to create the JsonObject.
                .Select(n => json.PropertyValueSafely<string>(n, options: JsonSerializerOptions))
                //Find the First non-null/empty/whitespace value...
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

            //Retur the first value we find if it is not null and we allowed to overwrite or the current value is null/empty/whitespace!
            return value != null && (overwriteExisting || string.IsNullOrWhiteSpace(currentValue))
                ? value
                : currentValue;
        }

        protected virtual string DetectContentType(string payloadBody)
            => JsonHelpers.IsValidJson(payloadBody)
                ? MessageContentTypes.Json
                : MessageContentTypes.PlainText;
    }
}
