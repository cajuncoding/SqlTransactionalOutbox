using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using SqlTransactionalOutbox.AzureServiceBus;
using SqlTransactionalOutbox.SqlServer.MicrosoftDataNS;

namespace SqlTransactionalOutbox.SampleApp.AzureFunctions.Functions
{
    //******************************************************************************************
    // 2. PROCESSING & PUBLISHING Messages in the Sql Transactional Outbox to Azure Service Bus
    //******************************************************************************************
    public static class TransactionalOutboxAgentFunction
    {
        [FunctionName(nameof(TransactionalOutboxAgentFunction))]
        public static async Task Run([TimerTrigger("%TransactionalOutboxAgentCronSchedule%")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"Transactional Outbox Agent initiating process at: {DateTime.Now}");
            var configSettings = new SampleAppConfig();

            var azureServiceBusPublisher = new DefaultAzureServiceBusOutboxPublisher(
                configSettings.AzureServiceBusConnectionString,
                new AzureServiceBusPublishingOptions()
                {
                    SenderApplicationName = $"{typeof(TransactionalOutboxAgentFunction).Assembly.GetName().Name}.{nameof(TransactionalOutboxAgentFunction)}",
                    LogDebugCallback = (s) => log.LogDebug(s),
                    ErrorHandlerCallback = (e) => log.LogError(e, "Unexpected Exception occurred while Processing the Transactional Outbox.")
                }
            );

            var outboxProcessingOptions = new OutboxProcessingOptions()
            {
                //ItemProcessingBatchSize = 200, //Only process the top X items to keep this function responsive!
                FifoEnforcedPublishingEnabled = true, //The Service Bus Topic is Session Enabled so we must processes it with FIFO Processing Enabled!
                LogDebugCallback = (m) => log.LogDebug(m),
                ErrorHandlerCallback = (e) => log.LogError(e, "Transactional Outbox Processing Exception"),
                MaxPublishingAttempts = configSettings.OutboxMaxPublishingRetryAttempts,
                TimeSpanToLive = configSettings.OutboxMaxTimeToLiveTimeSpan
            };

            //************************************************************
            //*** Execute processing of the Transactional Outbox...
            //************************************************************
            await using var sqlConnection = new SqlConnection(configSettings.SqlConnectionString);
            await sqlConnection.OpenAsync().ConfigureAwait(false);

            await sqlConnection
                .ProcessPendingOutboxItemsAsync(azureServiceBusPublisher, outboxProcessingOptions)
                .ConfigureAwait(false);

            //************************************************************
            //*** Execute Cleanup of Historical Outbox Data...
            //************************************************************
            await sqlConnection
                .CleanupHistoricalOutboxItemsAsync(configSettings.OutboxHistoryToKeepTimeSpan)
                .ConfigureAwait(false);
        }
    }
}
