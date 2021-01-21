using System;

namespace SqlTransactionalOutbox.CustomExtensions
{
    public static class TimeSpanCustomExtensions
    {
        public static string ToElapsedTimeDescriptiveFormat(this TimeSpan timeSpan)
        {
            var descriptiveFormat = $"{timeSpan:hh\\h\\:mm\\m\\:ss\\s\\:fff\\m\\s}";
            return descriptiveFormat;
        }
    }
}