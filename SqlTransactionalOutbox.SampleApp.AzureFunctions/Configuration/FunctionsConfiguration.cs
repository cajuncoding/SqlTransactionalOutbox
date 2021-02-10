using System;

namespace SqlTransactionalOutbox.SampleApp.AzureFunctions
{
    public class FunctionsConfiguration
    {
        private const int _defaultMaxPublishingRetryAttempts = 25;
        private const int _defaultMaxPublishingTTLDays = 10;
        private const int _defaultHistoryToKeepDays = 30;

        static FunctionsConfiguration()
        {
            SqlConnectionString = GetStringValue(nameof(SqlConnectionString));
            AzureServiceBusConnectionString = GetStringValue(nameof(AzureServiceBusConnectionString));
            OutboxMaxPublishingRetryAttempts = GetIntValue(nameof(OutboxMaxPublishingRetryAttempts), _defaultMaxPublishingRetryAttempts);
            OutboxMaxTimeToLiveTimeSpan = TimeSpan.FromDays(GetIntValue("OutboxMaxTimeToLiveDays", _defaultMaxPublishingTTLDays));
            OutboxHistoryToKeepTimeSpan = TimeSpan.FromDays(GetIntValue("OutboxHistoryToKeepDays", _defaultMaxPublishingTTLDays));
        }

        private static string GetStringValue(string key)
        {
            var value = Environment.GetEnvironmentVariable(key)?.Trim();
            return value;
        }

        private static int GetIntValue(string key, int defaultValue = default)
        {
            var value = int.TryParse(GetStringValue(key), out int intValue)
                ? intValue
                : defaultValue;
            
            return value;
        }

        public static string SqlConnectionString { get; }
        public static string AzureServiceBusConnectionString { get; }
        public static int OutboxMaxPublishingRetryAttempts { get; }
        public  static TimeSpan OutboxMaxTimeToLiveTimeSpan { get; }
        public static TimeSpan OutboxHistoryToKeepTimeSpan { get; }
    }
}
