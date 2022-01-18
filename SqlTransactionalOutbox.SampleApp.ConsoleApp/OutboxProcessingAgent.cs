using System;
using SqlTransactionalOutbox.AzureServiceBus;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.SampleApp.AzureFunctions;
using SqlTransactionalOutbox.SqlServer.MicrosoftDataNS;

namespace SqlTransactionalOutbox.SampleApp.ConsoleApp
{
    /// <summary>
    ///******************************************************************************************
    /// 2. PROCESSING & PUBLISHING Messages in the Sql Transactional Outbox to Azure Service Bus
    ///******************************************************************************************
    /// </summary>
    public class OutboxProcessor : IAsyncDisposable
    {
        //  NOTE: this is AsyncDisposable!
        protected ISqlTransactionalOutboxPublisher<Guid> OutboxPublisher { get; set; }

        //  NOTE: this is AsyncDisposable!
        protected AsyncThreadOutboxProcessingAgent OutboxProcessingAgent { get; set; }

        public OutboxProcessor(SampleAppConfig configSettings)
        {
            configSettings.AssertNotNull(nameof(configSettings));

            var errorHandlerCallback = new Action<Exception>((e) =>
            {
                Console.WriteLine($"  ERROR => {e.GetMessagesRecursively()}");
                OutboxHelpers.DefaultLogErrorCallback(e);
            });

            //We Need a Publisher to publish Outbox Items...
            //  NOTE: this is AsyncDisposable so we Keep a Reference for Disposal!
            OutboxPublisher = new DefaultAzureServiceBusOutboxPublisher(
                configSettings.AzureServiceBusConnectionString,
                new AzureServiceBusPublishingOptions()
                {
                    SenderApplicationName = typeof(OutboxHelpers).Assembly.GetName().Name,
                    LogDebugCallback = OutboxHelpers.DefaultLogDebugCallback,
                    ErrorHandlerCallback = errorHandlerCallback
                }
            );

            //Finally We Need the Processing Agent to process the Outbox on a background (Async) thread...
            //  NOTE: this is AsyncDisposable so we Keep a Reference for Disposal!
            OutboxProcessingAgent = new AsyncThreadOutboxProcessingAgent(
                TimeSpan.FromSeconds(20),
                TimeSpan.FromDays(1),
                configSettings.SqlConnectionString,
                OutboxPublisher,
                //We Need Processing Options for the Agent...
                outboxProcessingOptions: new OutboxProcessingOptions()
                {
                    //ItemProcessingBatchSize = 200, //Only process the top X items to keep this function responsive!
                    FifoEnforcedPublishingEnabled = true, //The Service Bus Topic is Session Enabled so we must processes it with FIFO Processing Enabled!
                    LogDebugCallback = OutboxHelpers.DefaultLogDebugCallback,
                    ErrorHandlerCallback = errorHandlerCallback,
                    MaxPublishingAttempts = configSettings.OutboxMaxPublishingRetryAttempts,
                    TimeSpanToLive = configSettings.OutboxMaxTimeToLiveTimeSpan
                }
            );

        }

        public async Task StartProcessingAsync()
        {
            //RUN The ProcessingAgent!
            await OutboxProcessingAgent.StartAsync();
        }
        public async Task<long> StopProcessingAsync()
        {
            //RUN The ProcessingAgent!
            return await OutboxProcessingAgent.StopAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await StopProcessingAsync();
            await this.OutboxProcessingAgent.DisposeAsync();
            await this.OutboxPublisher.DisposeAsync();
        }
    }
}
