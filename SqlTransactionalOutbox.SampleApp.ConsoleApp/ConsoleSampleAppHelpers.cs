#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlTransactionalOutbox.AzureServiceBus;
using SqlTransactionalOutbox.SqlServer.MicrosoftDataNS;

namespace SqlTransactionalOutbox.SampleApp.ConsoleApp
{
    public static class OutboxFactory
    {
        public static Action<string> DefaultLogDebugCallback = (s) => Debug.WriteLine(s);
        public static Action<Exception> DefaultLogErrorCallback = (e) => Debug.WriteLine(e, "Unexpected Exception occurred while Processing the Transactional Outbox.");


        public static ISqlTransactionalOutboxPublisher<Guid> CreateAzureServiceBusOutboxPublisher(
            string azureServiceBusConnectionString,
            Action<string> logDebugCallback = null,
            Action<Exception> logErrorCallback = null
        )
        {
            var outboxPublisher = new DefaultAzureServiceBusOutboxPublisher(
                azureServiceBusConnectionString,
                new AzureServiceBusPublishingOptions()
                {
                    SenderApplicationName = typeof(OutboxFactory).Assembly.GetName().Name,
                    LogDebugCallback = logDebugCallback ?? DefaultLogDebugCallback,
                    LogErrorCallback = logErrorCallback ?? DefaultLogErrorCallback
                }
            );
            
            return outboxPublisher;
        }

        public static OutboxProcessingOptions CreateOutboxProcessingOptions(
            int outboxItemMaxPublishingRetryAttempts,
            TimeSpan outboxItemTimeToLive,
            Action<string> logDebugCallback = null,
            Action<Exception> logErrorCallback = null
        )
        {
            var outboxProcessingOptions = new OutboxProcessingOptions()
            {
                //ItemProcessingBatchSize = 200, //Only process the top X items to keep this function responsive!
                FifoEnforcedPublishingEnabled = true, //The Service Bus Topic is Session Enabled so we must processes it with FIFO Processing Enabled!
                LogDebugCallback = logDebugCallback ?? DefaultLogDebugCallback,
                LogErrorCallback = logErrorCallback ?? DefaultLogErrorCallback,
                MaxPublishingAttempts = outboxItemMaxPublishingRetryAttempts,
                TimeSpanToLive = outboxItemTimeToLive
            };

            return outboxProcessingOptions;
        }
    }
}
