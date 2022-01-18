#nullable disable
using System;
using System.Runtime.CompilerServices;

namespace SqlTransactionalOutbox.SampleApp.AzureFunctions
{
    //Always a good idea to abstract away or encapsulate the core/base reading of config values...
    public class SampleAppConfig
    {
        private const int _defaultMaxPublishingRetryAttempts = 25;
        private const int _defaultMaxPublishingTTLDays = 10;
        private const int _defaultHistoryToKeepDays = 30;

        private const string _defaultServiceBusTopic = "SqlTransactionalOutbox/Integration-Tests";
        private const string _defaultServiceBusSubscription = "dev-local";

        public SampleAppConfig()
        {
            SqlConnectionString = GetStringValue(nameof(SqlConnectionString));
            AzureServiceBusConnectionString = GetStringValue(nameof(AzureServiceBusConnectionString));
            AzureServiceBusTopic = GetStringValue(nameof(AzureServiceBusTopic), _defaultServiceBusTopic);
            AzureServiceBusSubscription = GetStringValue(nameof(AzureServiceBusSubscription), _defaultServiceBusSubscription);
            OutboxMaxPublishingRetryAttempts = GetIntValue(nameof(OutboxMaxPublishingRetryAttempts), _defaultMaxPublishingRetryAttempts);
            OutboxMaxTimeToLiveTimeSpan = TimeSpan.FromDays(GetIntValue("OutboxMaxTimeToLiveDays", _defaultMaxPublishingTTLDays));
            OutboxHistoryToKeepTimeSpan = TimeSpan.FromDays(GetIntValue("OutboxHistoryToKeepDays", _defaultHistoryToKeepDays));
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
    }
}
