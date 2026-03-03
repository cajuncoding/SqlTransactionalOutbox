using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SqlTransactionalOutbox.AzureServiceBus;
using SqlTransactionalOutbox.SqlServer.MicrosoftDataNS;
using SqlAppLockHelper.MicrosoftDataNS;
using System.Threading;
using Functions.Worker.AddOns.Common;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox.SampleApp.AzureFunctions.Functions
{
    //******************************************************************************************
    // 2. PROCESSING & PUBLISHING Messages in the Sql Transactional Outbox to Azure Service Bus
    //******************************************************************************************
    public static class TransactionalOutboxAgentFunction
    {
        public static string DistributedLockName { get; } = $"SqlTransactionalOutbox.{nameof(TransactionalOutboxAgentFunction)}.DistributedLock";

        [Function(nameof(TransactionalOutboxAgentFunction))]
        public static async Task Run(
            [TimerTrigger("%TransactionalOutboxAgentCronSchedule%")]TimerInfo timerInfo,
            FunctionContext functionContext,
            CancellationToken cancellationToken
        )
        {
            var logger = functionContext.GetLogger();
            logger.LogInformation($"Transactional Outbox Agent initiating process at: {DateTime.Now}");
            
            var configSettings = new SampleAppConfig();

            var azureServiceBusPublisher = new DefaultAzureServiceBusOutboxPublisher(
                configSettings.AzureServiceBusConnectionString,
                new AzureServiceBusPublishingOptions()
                {
                    SenderApplicationName = $"{typeof(TransactionalOutboxAgentFunction).Assembly.GetName().Name}.{nameof(TransactionalOutboxAgentFunction)}",
                    LogDebugCallback = (s) => logger.LogDebug(s),
                    ErrorHandlerCallback = (e) => logger.LogError(e, "Unexpected Exception occurred while Processing the Transactional Outbox.")
                }
            );

            //NOTE: We use local options (not Global defaults) so that we can easily provide our own Logging Callbacks to seamless integrate with our Azure Function log streaming
            var outboxProcessingOptions = new OutboxProcessingOptions()
            {
                //ItemProcessingBatchSize = 200, //Only process the top X items to keep this function responsive!
                FifoEnforcedPublishingEnabled = true, //The Service Bus Topic is Session Enabled so we must processes it with FIFO Processing Enabled!
                MaxPublishingAttempts = configSettings.OutboxMaxPublishingRetryAttempts,
                TimeSpanToLive = configSettings.OutboxMaxTimeToLiveTimeSpan,
                LogDebugCallback = (m) => logger.LogDebug(m),
                ErrorHandlerCallback = (e) => logger.LogError(e, "Transactional Outbox Processing Exception")
            };

            //**********************************************************************************************************
            //*** Execute processing of the Transactional Outbox...
            //*** NOTE: It is a good practice to ensure only one processing agent runs at any time which
            //***     can be achieved easily with SQL Server using a distributed application mutex lock!
            //*** NOTE: The Distributed Lock is AsyncDisposable and must be disposed to release the lock
            //***     which is elegantly done with the use of "await using" to ensures proper disposal
            //***     and release of the lock even in the case of exceptions!
            //**********************************************************************************************************
            await using var sqlConnection = new SqlConnection(configSettings.SqlConnectionString);
            await sqlConnection.OpenAsync(cancellationToken);

            await using (var sqlDistributedLock = await sqlConnection.AcquireAppLockAsync(DistributedLockName, throwsException: false))
            {
                //************************************************************
                //*** First Execute Processing of Pending Outbox Items...
                //************************************************************
                var outboxResults = await sqlConnection.ProcessPendingOutboxItemsAsync(azureServiceBusPublisher, outboxProcessingOptions);

                logger.LogInformation($"Processed [{outboxResults.ProcessedItemsCount}] Outbox items in {outboxResults.ProcessingTimer.ToElapsedTimeDescriptiveFormat()}...");
                logger.LogInformation($"  - [{outboxResults.SuccessfullyPublishedItems.Count}] Succssfully Published");
                logger.LogInformation($"  - [{outboxResults.FailedItems.Count}] Failed Items");

                //************************************************************
                //*** Second Execute Cleanup of Historical Outbox Data...
                //************************************************************
                await sqlConnection.CleanupHistoricalOutboxItemsAsync(configSettings.OutboxHistoryToKeepTimeSpan);
            }
        }
    }
}
