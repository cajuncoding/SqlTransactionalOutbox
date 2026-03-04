using System;

namespace SqlTransactionalOutbox.SampleApp.AzureFunctions
{
    public interface ISampleAppConfig
    {
        string AzureServiceBusConnectionString { get; }
        string AzureServiceBusSubscription { get; }
        string AzureServiceBusTopic { get; }
        TimeSpan OutboxHistoryToKeepTimeSpan { get; }
        int OutboxMaxPublishingRetryAttempts { get; }
        TimeSpan OutboxMaxTimeToLiveTimeSpan { get; }
        TimeSpan OutboxProcessingIntervalTimeSpan { get; }
        string SqlConnectionString { get; }
    }
}