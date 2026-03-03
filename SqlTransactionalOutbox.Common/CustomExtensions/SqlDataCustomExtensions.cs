using SqlTransactionalOutbox.Caching;
using System;
using System.Data;

namespace SqlTransactionalOutbox.CustomExtensions
{
    public static class SqlDataCustomExtensions
    {
        //A safe, yet still very high performance method for reading values more easily from an IDataRecord or IDataReader 
        //  (e.g. SqlDataReader) with proper NULL handling and zero boxing for common value types.
        public static T GetValueSafely<T>(this IDataRecord record, int ordinal)
        {
            if (record is null)
                throw new ArgumentNullException(nameof(record));

            Type targetType = typeof(T);
            Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            // NULL handling
            if (record.IsDBNull(ordinal))
            {
                if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
                    throw new InvalidOperationException($"The value at column ordinal [{ordinal}] is NULL, but the target type '{targetType.Name}' is not nullable.");

                return default!;
            }

            // FAST TYPED PATHS (zero boxing except unavoidable cast)
            if (underlyingType == TypeCache.String) return (T)(object)record.GetString(ordinal);
            if (underlyingType == TypeCache.DateTime) return (T)(object)record.GetDateTime(ordinal);
            if (underlyingType == TypeCache.Boolean) return (T)(object)record.GetBoolean(ordinal);
            if (underlyingType == TypeCache.Int32) return (T)(object)record.GetInt32(ordinal);
            if (underlyingType == TypeCache.Int64) return (T)(object)record.GetInt64(ordinal);
            if (underlyingType == TypeCache.Int16) return (T)(object)record.GetInt16(ordinal);
            if (underlyingType == TypeCache.Float) return (T)(object)record.GetFloat(ordinal);
            if (underlyingType == TypeCache.Double) return (T)(object)record.GetDouble(ordinal);
            if (underlyingType == TypeCache.Decimal) return (T)(object)record.GetDecimal(ordinal);
            if (underlyingType == TypeCache.Guid) return (T)(object)record.GetGuid(ordinal);
            if (underlyingType == TypeCache.Byte) return (T)(object)record.GetByte(ordinal);
            if (underlyingType == TypeCache.ByteArray) return (T)record.GetValue(ordinal);

            // UNIVERSAL FALLBACK (still supports all types)
            //NOTE: Other helpful types like DateTimeOffset are not natively supported by IDataRecord,
            //      so we rely on the universal GetValue() method for those types which will require (only) an additional cast
            //      and potential boxing for value types, but this is a necessary fallback to support all possible types.
            return (T)record.GetValue(ordinal);
        }
    }
}
