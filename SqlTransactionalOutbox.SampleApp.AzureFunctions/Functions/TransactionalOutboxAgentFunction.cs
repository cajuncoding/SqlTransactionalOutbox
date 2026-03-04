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
    public class TransactionalOutboxAgentFunction(ISampleAppConfig appConfig)
    {
        public static string DistributedLockName { get; } = $"SqlTransactionalOutbox.{nameof(TransactionalOutboxAgentFunction)}.DistributedLock";

        [Function(nameof(TransactionalOutboxAgentFunction))]
        public async Task Run(
            [TimerTrigger("%TransactionalOutboxAgentCronSchedule%")]TimerInfo timerInfo,
            FunctionContext functionContext,
            CancellationToken cancellationToken
        )
        {
            var logger = functionContext.GetLogger();
            logger.LogInformation($"Transactional Outbox Agent initiating process at: {DateTime.Now}");

            //**********************************************************************************************************
            //*** Execute processing of the Transactional Outbox...
            //*** NOTE: It is a (usually) desirable to ensure only one processing agent runs at any time which
            //***     can be achieved easily with SQL Server using a distributed application mutex lock!
            //*** NOTE: The Distributed Lock is AsyncDisposable and must be disposed to release the lock
            //***     which is elegantly done with the use of "await using" to ensures proper disposal
            //***     and release of the lock even in the case of exceptions!
            //**********************************************************************************************************
            await using var sqlConnection = new SqlConnection(appConfig.SqlConnectionString);
            await sqlConnection.OpenAsync(cancellationToken);

            await using (var sqlDistributedLock = await sqlConnection.AcquireAppLockAsync(DistributedLockName, throwsException: false))
            {
                //Initiallize our Outbox Publisher instance...
                //NOTE: We don't use DI here because we want to provide our own Logging & Error Handler Callbacks that integrate with our
                //  current Azure Function log streaming and illustrate the process of creating the Publisher instance directly; in a production
                //  implementation you may choose to use DI and provide the Callbacks via configuration or other means.
                //  For example the Functions.Worker.ILoggerSupport library enables seamless ILogger (non-generic) injection which would allow this to fully use DI...
                //  But this works just fine 👍 . . . 
                var azureServiceBusPublisher = new DefaultAzureServiceBusOutboxPublisher(
                    appConfig.AzureServiceBusConnectionString,
                    new AzureServiceBusPublishingOptions()
                    {
                        SenderApplicationName = $"{typeof(TransactionalOutboxAgentFunction).Assembly.GetName().Name}.{nameof(TransactionalOutboxAgentFunction)}",
                        LogDebugCallback = (s) => logger.LogDebug(s),
                        ErrorHandlerCallback = (e) => logger.LogError(e, "Unexpected Exception occurred while Processing the Transactional Outbox.")
                    }
                );

                //NOTE: We instantiate a new local options instance (not Global defaults) so that we can easily set/provide our own
                //  Logging Callbacks to seamless integrate with our Azure Function log streaming...
                //NOTE: By using the CreateOptions factory method we ensure that our instance inherits all global defaults already configured.
                var outboxProcessingOptions = OutboxProcessingOptions.CreateOptions(options =>
                {
                    options.LogDebugCallback = (m) => logger.LogDebug(m);
                    options.ErrorHandlerCallback = (e) => logger.LogError(e, "Transactional Outbox Processing Exception");
                });

                //************************************************************
                //*** First Execute Processing of Pending Outbox Items...
                //************************************************************
                var outboxResults = await sqlConnection.ProcessPendingOutboxItemsAsync(azureServiceBusPublisher, outboxProcessingOptions);

                logger.LogInformation($"Processed [{outboxResults.ProcessedItemsCount}] Outbox items in {outboxResults.ProcessingTimer.ToElapsedTimeDescriptiveFormat()}...");
                logger.LogInformation($"  - [{outboxResults.SuccessfullyPublishedItems.Count}] Successfully Published");
                logger.LogInformation($"  - [{outboxResults.FailedItems.Count}] Failed Items");

                //************************************************************
                //*** Second Execute Cleanup of Historical Outbox Data...
                //************************************************************
                await sqlConnection.CleanupHistoricalOutboxItemsAsync(appConfig.OutboxHistoryToKeepTimeSpan, cancellationToken: cancellationToken);
            }
        }
    }
}
