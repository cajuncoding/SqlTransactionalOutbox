using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SqlTransactionalOutbox.AzureServiceBus;
using SqlTransactionalOutbox.AzureServiceBus.Receiving;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox.SampleApp.AzureFunctions
{
    public class AzureServiceBusIntegrationTopicHandlerFunction
    {
        [FunctionName(nameof(AzureServiceBusIntegrationTopicHandlerFunction))]
        public Task Run(
            [ServiceBusTrigger(
                topicName: "%ServiceBus-IntegrationTest-Topic%",
                subscriptionName: "%ServiceBus-IntegrationTest-Subscription%", 
                //NOTE: Config Expression syntax not needed for Connection:
                Connection = "AzureServiceBusConnectionString",
                //NOTE: Sessions are used to support FIFO Enforced Processing with Azure Service Bus.
                IsSessionsEnabled = true
            )] Message serviceBusMessage,
            ILogger logger,
            CancellationToken cancellationToken
        )
        {
            var timer = Stopwatch.StartNew();
            try
            {
                var receivedItem = new DefaultAzureServiceBusReceivedItem<string>(serviceBusMessage);

                logger.LogInformation($"Azure Service Bus Message Received at [{DateTimeOffset.Now}]:" +
                    $"{Environment.NewLine}Label: [{receivedItem.AzureServiceBusMessage.Label}]" +
                    $"{Environment.NewLine}UniqueIdentifier: [{receivedItem.UniqueIdentifier}]" +
                    $"{Environment.NewLine}Content Type: [{receivedItem.ContentType}]" +
                    $"{Environment.NewLine}Correlation ID: [{receivedItem.CorrelationId}]" +
                    $"{Environment.NewLine}FIFO Grouping ID: [{receivedItem.FifoGroupingIdentifier}]" +
                    $"{Environment.NewLine}Created Date UTC: [{receivedItem.PublishedItem.CreatedDateTimeUtc}]" +
                    $"{Environment.NewLine}Publish Target: [{receivedItem.PublishedItem.PublishTarget}]" +
                    $"{Environment.NewLine}Publish Attempts: [{receivedItem.PublishedItem.PublishAttempts}]" +
                    $"{Environment.NewLine}Publish Status: [{receivedItem.PublishedItem.Status}]" +
                    $"{Environment.NewLine}Payload:{Environment.NewLine}{receivedItem.ParsePayloadBody()}" +
                    Environment.NewLine
                );

                timer.Stop();
                logger.LogInformation($"Message Processing completed at [{DateTimeOffset.Now}]" +
                                      $" in [{timer.Elapsed.ToElapsedTimeDescriptiveFormat()}].");

            }
            catch (Exception exc)
            {
                timer.Stop();
                //Throw an Exception so that AzureFunctions will 'Abandon()' the service message and enable
                //  retrying of Delivery again (until MaxDeliveryCount limit is reached)!
                throw new Exception(
                    $"An unexpected error was encountered during processing of the Azure Service Bus Message -- " +
                    $" [ID={serviceBusMessage.MessageId}] [Subject={serviceBusMessage.Label}] -- " +
                    $" after [{timer.Elapsed.ToElapsedTimeDescriptiveFormat()}] of processing time.",
                    exc
                );
            }

            return Task.CompletedTask;
        }
    }

}
