using System;

namespace SqlTransactionalOutbox.CustomExtensions
{
    public static class DateTimeCustomExtensions
    {
        public static string ToIso8601RoundTripFormat(this DateTime dateTime) => dateTime.ToString("O");
        public static string ToIso8601RoundTripFormat(this DateTimeOffset dateTimeOffset) => dateTimeOffset.ToString("O");
    }
}