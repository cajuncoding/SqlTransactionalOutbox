using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.JsonExtensions;
using SqlTransactionalOutbox.Publishing;
using SystemTextJsonHelpers;

namespace SqlTransactionalOutbox.Utilities
{
    public class PayloadBuilder
    {
        //TODO: CLEANUP
        //public static JsonSerializerSettings OutboxJsonSerializerSettings { get; set; } = new JsonSerializerSettings()
        //{
        //    ContractResolver = new DefaultContractResolver
        //    {
        //        NamingStrategy = new CamelCaseNamingStrategy()
        //    },
        //    NullValueHandling = NullValueHandling.Ignore,
        //    DateFormatHandling = DateFormatHandling.IsoDateFormat,
        //    // Ensure DateTimeOffset is used when reading
        //    DateParseHandling = DateParseHandling.DateTimeOffset,
        //    // Preserve whatever offset is in the JSON (Z, +00:00, -06:00, etc.)
        //    DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind
        //};

        //TODO: CLEANUP - Is this named well based on usage???
        public static JsonSerializerOptions OutboxJsonSerializerOptions { get; set; } = SystemTextJsonDefaults.CreateRelaxedJsonSerializerOptions(
            allowWritingNullValues: false    
        );

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
        public Dictionary<string, string> Headers { get; set; } = [];

        public static PayloadBuilder FromObject<TObject>(TObject obj)
        {
            var payload = new PayloadBuilder();

            var json = obj.ToJson(OutboxJsonSerializerOptions);
            payload.ApplyValues(json);

            return payload;
        }

        public static PayloadBuilder FromJsonSafely(string jsonText)
        {
            var json = JsonHelpers.ParseSafely(jsonText, OutboxJsonSerializerOptions);
            return new PayloadBuilder().ApplyValues(json as JsonObject);
        }

        public PayloadBuilder ApplyValues<TModel>(TModel model, bool overwriteExisting = true)
        {
            model.AssertNotNull(nameof(model));
            
            var json = model.ToJsonNode(OutboxJsonSerializerOptions);
            ApplyValues(json as JsonObject, overwriteExisting);

            return this;
        }

        //public PayloadBuilder ApplyValues(JsonObject json, bool overwriteExisting = true)
        //{
        //    json.AssertNotNull(nameof(json));

        //    //var jsonValuesCaseInsensitiveLookup = json.ToArray().ToLookup(
        //    //    p => p.Key,
        //    //    p => p.Value.GetValueKind() switch
        //    //    {
        //    //        JsonValueKind.Null => null,
        //    //        //Use ISO 8601 format for DateTime values to ensure consistency and proper parsing on the receiving end regardless of culture/locale settings.
        //    //        JTokenType.Date => json.ValueSafely<DateTimeOffset?>(p.Name)?.ToIso8601RoundTripFormat()
        //    //                            ?? json.ValueSafely<DateTime?>(p.Name)?.ToIso8601RoundTripFormat(),
        //    //        JTokenType.Boolean => p.Value.Value<bool>().ToString(),
        //    //        _ => p.Value.ToString()


        //    //        JsonValueKind.String when json.ValueSafely<DateTimeOffset?>(p.)?.ToIso8601RoundTripFormat()
        //    //                                ?? json.ValueSafely<DateTime?>(p.Name)?.ToIso8601RoundTripFormat(),
        //    //        JsonValueKind.Boolean => p.Value.Value<bool>().ToString(),
        //    //        _ => p.Value.ToString()
        //    //    },
        //    //    StringComparer.OrdinalIgnoreCase
        //    //);

        //    //Dynamically detect if the Json has a Body property (case-insensitive) and if not, then we will use the entire Json as the default Body content.
        //    //  This allows for maximum flexibility in how the JSON Payload can be structured allowing dynamic Message Property initialization (e.g. PublishTarget, To, Subject, etc.) 
        //    //  without forcing a specific structure on the JSON Payload...
        //    //NOTE: This is necessary for the downstream ApplyValues() to correctly initialize the Body value from the JSON.
        //    var jsonBodyProperty = json.Property(JsonMessageFields.Body, StringComparison.OrdinalIgnoreCase);
        //    if (jsonBodyProperty == null)
        //        json[JsonMessageFields.Body] = json.ToString(Formatting.None);

        //    //Apply all discrete values...
        //    ApplyValues(jsonValuesCaseInsensitiveLookup, overwriteExisting);

        //    //Headers are available via Json Payload as a nested Json object (Key/Value)...
        //    var jsonHeaders = json.ValueSafely<JObject>(JsonMessageFields.Headers)
        //        ?? json.ValueSafely<JObject>(JsonMessageFields.UserProperties);
            
        //    if (jsonHeaders != null)
        //    {
        //        this.Headers = new Dictionary<string, string>();
        //        //NOTE: We DO NOT convert headers to encoded names here for simplicity and to
        //        //      prevent duplicate header name construction/encoding.
        //        foreach (var prop in jsonHeaders.Properties())
        //        {
        //            this.Headers[prop.Name] = prop.Value.ToString();
        //        }
        //    }

        //    //Make Chainable...
        //    return this;
        //}

        public PayloadBuilder ApplyValues(JsonObject json, bool overwriteExisting = true)
        {
            json.AssertNotNull(nameof(json));

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
            Body = InitStringValue(Body, json, overwriteExisting, JsonMessageFields.Body);

            //Look for ContentType but then fallback to detect it if not specified explicitly...
            ContentType = InitStringValue(ContentType, json, overwriteExisting, JsonMessageFields.ContentType)
                ?? DetectContentType(Body);

            //Headers are available via Json Payload as a nested Json object (Key/Value)...
            var jsonHeaders = json[JsonMessageFields.Headers]?.AsObject()
                ?? json[JsonMessageFields.UserProperties]?.AsObject();

            if (jsonHeaders != null)
            {
                this.Headers ??= [];
                //NOTE: We DO NOT convert headers to encoded names here for simplicity and to
                //      prevent duplicate header name construction/encoding.
                foreach (var propName in jsonHeaders.GetPropertyNames())
                    this.Headers[propName] = jsonHeaders.ValueSafely<string>(propName);
            }

            //Make Chainable...
            return this;
        }

        /// <summary>
        /// Build a valid JObject from this Payload with standard default properties...
        /// </summary>
        /// <returns></returns>
        public JsonObject ToJsonObject() => this.ToJsonNode(OutboxJsonSerializerOptions) as JsonObject;

        protected virtual string InitStringValue(string currentValue, JsonObject json, bool overwriteExisting, params string[] fieldNames)
        {
            string value = fieldNames?
                //Map all specified names to potentially existing values in our json (or null)
                //NOTE: The JsonObject should be case-insensitive as set in the relaxed JsonSerializerOptions used to create the JsonObject.
                .Select(n => json.ValueSafely<string>(n))
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
