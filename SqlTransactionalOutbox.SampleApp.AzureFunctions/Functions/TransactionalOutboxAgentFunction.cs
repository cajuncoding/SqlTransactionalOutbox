using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using SqlTransactionalOutbox.AzureServiceBus;
using SqlTransactionalOutbox.SqlServer.MicrosoftDataNS;

namespace SqlTransactionalOutbox.SampleApp.AzureFunctions.Functions
{
    public static class TransactionalOutboxAgentFunction
    {
        [FunctionName(nameof(TransactionalOutboxAgentFunction))]
        public static async Task Run([TimerTrigger("%TransactionalOutboxAgentCronSchedule%")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"Transactional Outbox Agent initiating process at: {DateTime.Now}");

            var azureServiceBusPublisher = new DefaultAzureServiceBusOutboxPublisher(
                FunctionsConfiguration.AzureServiceBusConnectionString,
                new AzureServiceBusPublishingOptions()
                {
                    SenderApplicationName = $"{typeof(TransactionalOutboxAgentFunction).Assembly.GetName().Name}.{nameof(TransactionalOutboxAgentFunction)}",
                    LogDebugCallback = (s) => log.LogDebug(s),
                    LogErrorCallback = (e) => log.LogError(e, "Unexpected Exception occurred while Processing the Transactional Outbox.")
                }
            );

            var outboxProcessingOptions = new OutboxProcessingOptions()
            {
                //ItemProcessingBatchSize = 200, //Only process the top X items to keep this function responsive!
                FifoEnforcedPublishingEnabled = true, //The Service Bus Topic is Session Enabled so we must processes it with FIFO Processing Enabled!
                LogDebugCallback = (m) => log.LogDebug(m),
                LogErrorCallback = (e) => log.LogError(e, "Transactional Outbox Processing Exception"),
                MaxPublishingAttempts = FunctionsConfiguration.OutboxMaxPublishingRetryAttempts,
                TimeSpanToLive = FunctionsConfiguration.OutboxMaxTimeToLiveTimeSpan
            };

            //************************************************************
            //*** Execute processing of the Transactional Outbox...
            //************************************************************
            var sqlConnection = new SqlConnection(FunctionsConfiguration.SqlConnectionString);
            await sqlConnection.OpenAsync().ConfigureAwait(false);

            await sqlConnection
                .ProcessPendingOutboxItemsAsync(azureServiceBusPublisher, outboxProcessingOptions)
                .ConfigureAwait(false);

            //************************************************************
            //*** Execute Cleanup of Historical Outbox Data...
            //************************************************************
            await sqlConnection
                .CleanupHistoricalOutboxItemsAsync(FunctionsConfiguration.OutboxHistoryToKeepTimeSpan)
                .ConfigureAwait(false);
        }
    }
}
