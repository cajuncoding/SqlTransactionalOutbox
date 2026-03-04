#nullable disable
using System;

namespace SqlTransactionalOutbox.SampleApp.AzureFunctions
{
    //Always a good idea to abstract away or encapsulate the core/base reading of config values...
    public class SampleAppConfig : ISampleAppConfig
    {
        public SampleAppConfig()
        {
            //Required...
            SqlConnectionString = GetStringValue(nameof(SqlConnectionString));
            AzureServiceBusConnectionString = GetStringValue(nameof(AzureServiceBusConnectionString));
            //Optional with Defaults...
            AzureServiceBusTopic = GetStringValue(nameof(AzureServiceBusTopic), "SqlTransactionalOutbox/Integration-Tests");
            AzureServiceBusSubscription = GetStringValue(nameof(AzureServiceBusSubscription), "dev-local");
            OutboxMaxPublishingRetryAttempts = GetIntValue(nameof(OutboxMaxPublishingRetryAttempts), 25);
            OutboxMaxTimeToLiveTimeSpan = TimeSpan.FromDays(GetIntValue("OutboxMaxTimeToLiveDays", 10));
            OutboxHistoryToKeepTimeSpan = TimeSpan.FromDays(GetIntValue("OutboxHistoryToKeepDays", 30));
            OutboxProcessingIntervalTimeSpan = TimeSpan.FromSeconds(GetIntValue("OutboxProcessingIntervalSeconds", 20));
        }

        //Always a good idea to abstract away or encapsulate the core/base reading of config values...
        private string ReadConfigValue(string key)
        {
            return Environment.GetEnvironmentVariable(key);
        }

        private string GetStringValue(string key, string defaultValue = null)
        {
            var value = ReadConfigValue(key)?.Trim();
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        private int GetIntValue(string key, int defaultValue = default)
        {
            var value = int.TryParse(GetStringValue(key), out int intValue)
                ? intValue
                : defaultValue;

            return value;
        }

        public string SqlConnectionString { get; }
        public string AzureServiceBusConnectionString { get; }
        public string AzureServiceBusTopic { get; }
        public string AzureServiceBusSubscription { get; }
        public int OutboxMaxPublishingRetryAttempts { get; }
        public TimeSpan OutboxMaxTimeToLiveTimeSpan { get; }
        public TimeSpan OutboxHistoryToKeepTimeSpan { get; }
        public TimeSpan OutboxProcessingIntervalTimeSpan { get; }
    }
}
