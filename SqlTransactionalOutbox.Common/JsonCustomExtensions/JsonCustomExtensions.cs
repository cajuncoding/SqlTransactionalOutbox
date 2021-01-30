using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SqlTransactionalOutbox.JsonExtensions
{
    public static class JsonHelpers
    {
        public static JObject ParseSafely(string jsonText)
        {
            try
            {
                var json = JObject.Parse(jsonText);
                return json;
            }
            catch (Exception)
            {
                //DO NOTHING
                return null;
            }
        }

        //Helpful method inspired from StackOverflow here:
        //  https://stackoverflow.com/a/14977915/7293142
        public static bool IsValidJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) 
                return false;
            
            text = text.Trim();
            if ((text.StartsWith("{") && text.EndsWith("}")) //For object
                || (text.StartsWith("[") && text.EndsWith("]"))) //For array
            {
                try
                {
                    var obj = JToken.Parse(text);
                    return true;
                }
                catch (JsonReaderException jex)
                {
                    #if DEBUG
                    Debug.WriteLine(jex.Message);
                    #endif
                }
                catch (Exception ex) //some other exception
                {
                    #if DEBUG
                    Debug.WriteLine(ex.ToString());
                    #endif
                }
            }

            return false;
        }
    }

    public static class JsonCustomExtensions
    {
        public static TValue ValueSafely<TValue>(this JObject json, string fieldName, TValue defaultValue = default)
        {
            if (json == null) return defaultValue;

            var jToken = json.GetValue(fieldName, StringComparison.OrdinalIgnoreCase);
            var value = jToken == null
                ? defaultValue
                : jToken.Value<TValue>();

            return value;
        }
    }
}
