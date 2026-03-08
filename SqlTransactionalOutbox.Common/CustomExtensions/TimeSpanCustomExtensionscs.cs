using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SqlTransactionalOutbox.CustomExtensions
{
    public static class TimeSpanCustomExtensions
    {
        public static string ToElapsedTimeDescriptiveFormat(this Stopwatch timer)
            => timer.Elapsed.ToElapsedTimeDescriptiveFormat();

        public static string ToElapsedTimeDescriptiveFormat(this TimeSpan timeSpan)
            => $"{timeSpan:hh\\h\\:mm\\m\\:ss\\s\\:fff\\m\\s}";

        private static readonly TimeSpan _regexDefaultTimeout = TimeSpan.FromSeconds(5);

        private static readonly Regex _simpleUnitsTimeSpanRegex = new Regex(
            @"^(?<Value>[+-]?\d+(?:[.,]\d+)?)\s*(?<Unit>[smhd])?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            _regexDefaultTimeout
        );

        public static bool TryParseTimeSpanWithUnitsAndMinutesDefault(this string input, out TimeSpan result, IFormatProvider provider = null)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(input)) return false;

            var sanitizedInput = input.Trim();

            // 1) Try to parse with simple unit suffixes or raw integer value first (no units default to minutes)...
            var match = _simpleUnitsTimeSpanRegex.Match(sanitizedInput);
            var valueMatch = match.Groups["Value"];
            if (valueMatch.Success && double.TryParse(valueMatch.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                char unitChar = 'm'; // Default to minutes if no unit is specified
                var unitMatch = match.Groups["Unit"];
                if (unitMatch.Success && unitMatch.Value.Length == 1)
                    unitChar = char.ToLowerInvariant(unitMatch.Value[0]);

                try
                {
                    result = unitChar switch
                    {
                        's' => TimeSpan.FromSeconds(value),
                        'm' => TimeSpan.FromMinutes(value),
                        'h' => TimeSpan.FromHours(value),
                        'd' => TimeSpan.FromDays(value),
                        _ => TimeSpan.FromMinutes(value)
                    };

                    return true;
                }
                catch (OverflowException) { return false; }
            }

            // 2) Fall back to standard TimeSpan parsing
            return TimeSpan.TryParse(sanitizedInput, provider ?? CultureInfo.CurrentCulture, out result);
        }
    }
}