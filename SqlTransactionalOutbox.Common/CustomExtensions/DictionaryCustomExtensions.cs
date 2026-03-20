using System;
using System.Collections.Generic;


namespace SqlTransactionalOutbox.CustomExtensions
{
    public static class DictionaryCustomExtensions
    {
        public static T GetValueSafely<T>(this IReadOnlyDictionary<string, object> dictionary, string key)
        {
            if (dictionary != null && !string.IsNullOrEmpty(key) && dictionary.TryGetValue(key, out var value))
                return (T)value;
            else
                return default(T);
        }
    }
}
