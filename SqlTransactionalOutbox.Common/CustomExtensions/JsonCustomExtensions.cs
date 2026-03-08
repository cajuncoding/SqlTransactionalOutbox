using SqlTransactionalOutbox.CustomExtensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using SystemTextJsonHelpers;

namespace SqlTransactionalOutbox.JsonExtensions
{
    public static class JsonHelpers
    {
        public static JsonNode ParseSafely(string jsonText, JsonSerializerOptions jsonSerializerOptions = null)
        {
            try
            {
                if (IsDuckTypedJson(jsonText))
                    return jsonText.ToJsonNode(jsonSerializerOptions);
            }
            catch (Exception)
            {
                //DO NOTHING
            }

            return null;
        }

        public static bool IsDuckTypedJson(string jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText))
                return false;

            var text = jsonText.Trim();
            if ((text.StartsWith("{") && text.EndsWith("}")) //For object
                || (text.StartsWith("[") && text.EndsWith("]"))) //For array
            {
                return true;
            }

            return false;
        }

        //Helpful method inspired from StackOverflow here:
        //  https://stackoverflow.com/a/14977915/7293142
        public static bool IsValidJson(string jsonText)
        {
            if (IsDuckTypedJson(jsonText))
            {
                try
                {
                    var obj = jsonText.ToJsonNode();
                    return true;
                }
                catch (Exception ex)
                {
                    #if DEBUG
                    Debug.WriteLine(ex.GetMessagesRecursively());
                    #endif
                }
            }

            return false;
        }
    }

    public static class JsonCustomExtensions
    {
        private static readonly JsonSerializerOptions _relaxedJsonSerializerOptions = SystemTextJsonDefaults.CreateRelaxedJsonSerializerOptions();

        public static IEnumerable<string> GetPropertyNames(this JsonObject json)
            => json?.Select(kvp => kvp.Key) ?? Enumerable.Empty<string>();

        public static TValue ValueSafely<TValue>(this JsonObject json, string fieldName, TValue defaultValue = default)
            => ValueSafelyInternal(json?[fieldName], defaultValue);

        public static TValue ValueSafely<TValue>(this JsonValue jsonValueNode, TValue defaultValue = default)
            => ValueSafelyInternal(jsonValueNode, defaultValue);

        private static TValue ValueSafelyInternal<TValue>(JsonNode jsonNode, TValue defaultValue = default)
        {
            try
            {
                return jsonNode switch
                {
                    //Short circuit for null case to avoid unnecessary allocations & processing...
                    null => defaultValue,
                    //For primitives JsonValue.TryGetValue<TValue> is fast...
                    JsonValue valueNode when valueNode.TryGetValue<TValue>(out var primitive) => primitive,
                    //Fallback to System.Text.Json deserialization
                    JsonNode node => node.Deserialize<TValue>(_relaxedJsonSerializerOptions) ?? defaultValue,
                };
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}
