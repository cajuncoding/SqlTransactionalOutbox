using System;

namespace SqlTransactionalOutbox.CustomExtensions
{
    public static class DateTimeCustomExtensions
    {
        public static string ToIso8601RoundTripFormat(this DateTime dateTime) => dateTime.ToString("O");
        public static string ToIso8601RoundTripFormat(this DateTime? dateTime) => dateTime?.ToIso8601RoundTripFormat();
        public static string ToIso8601RoundTripFormat(this DateTimeOffset dateTimeOffset) => dateTimeOffset.ToString("O");
        public static string ToIso8601RoundTripFormat(this DateTimeOffset? dateTimeOffset) => dateTimeOffset?.ToIso8601RoundTripFormat();

        public static DateTimeOffset ToDateTimeOffsetAsUtc(this DateTime dateTime) => new DateTimeOffset(dateTime, TimeSpan.Zero);
        public static DateTimeOffset? ToDateTimeOffsetAsUtc(this DateTime? dateTime) => dateTime?.ToDateTimeOffsetAsUtc();
        public static DateTimeOffset ToDateTimeOffsetAsUtc(this DateTimeOffset dateTimeOffset) => dateTimeOffset.UtcDateTime.ToDateTimeOffsetAsUtc();
        public static DateTimeOffset? ToDateTimeOffsetAsUtc(this DateTimeOffset? dateTimeOffset) => dateTimeOffset?.ToDateTimeOffsetAsUtc();
    }
}