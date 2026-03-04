using System;

namespace SqlTransactionalOutbox.Caching
{
    public static class TypeCache
    {
        public static readonly Type Int32 = typeof(int);
        public static readonly Type Int64 = typeof(long);
        public static readonly Type Int16 = typeof(short);
        public static readonly Type Boolean = typeof(bool);
        public static readonly Type String = typeof(string);
        public static readonly Type DateTime = typeof(DateTime);
        public static readonly Type Guid = typeof(Guid);
        public static readonly Type Decimal = typeof(decimal);
        public static readonly Type Double = typeof(double);
        public static readonly Type Float = typeof(float);
        public static readonly Type Byte = typeof(byte);
        public static readonly Type ByteArray = typeof(byte[]);
    }
}
