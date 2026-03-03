using System;
using System.Diagnostics;

namespace SqlTransactionalOutbox.CustomExtensions
{
    public static class TimeSpanCustomExtensions
    {
        public static string ToElapsedTimeDescriptiveFormat(this Stopwatch timer)
            => timer.Elapsed.ToElapsedTimeDescriptiveFormat();

        public static string ToElapsedTimeDescriptiveFormat(this TimeSpan timeSpan)
            => $"{timeSpan:hh\\h\\:mm\\m\\:ss\\s\\:fff\\m\\s}";
    }
}