using SqlTransactionalOutbox.CustomExtensions;
using System;
using System.Diagnostics;
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
                    return jsonText.FromJsonTo<JsonNode>(jsonSerializerOptions);
            }
            catch (Exception)
            {
                //DO NOTHING
            }

            return null;
        }

        public static bool IsDuckTypedJson(string jsonText)
            => jsonText.IsDuckTypedJson();

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
}
