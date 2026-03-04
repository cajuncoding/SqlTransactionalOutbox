using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Functions.Worker.AddOns.Common;
using SqlTransactionalOutbox.AzureServiceBus;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox.SampleApp.AzureFunctions
{
    //******************************************************************************************
    // 3. RECEIVING Messages that were Published via AzureServiceBus
    //******************************************************************************************
    public class TransactionalOutboxFifoReceiverFunction
    {
        [Function(nameof(TransactionalOutboxFifoReceiverFunction))]
        public Task Run(
            [ServiceBusTrigger(
                topicName: "%AzureServiceBusTopic%",
                subscriptionName: "%AzureServiceBusSubscription%", 
                //NOTE: Config Expression syntax not needed for Connection:
                Connection = "AzureServiceBusConnectionString",
                //NOTE: Sessions are used to support FIFO Enforced Processing with Azure Service Bus.
                IsSessionsEnabled = true,
                //For simplicity in this Demo we use Autocompletion; exceptions will result in Abandoning and Retrying Events.
                AutoCompleteMessages = true
            )] ServiceBusReceivedMessage serviceBusMessage,
            FunctionContext functionContext
        )
        {
            var logger = functionContext.GetLogger();
            var timer = Stopwatch.StartNew();
            
            try
            {
                var receivedItem = serviceBusMessage.ToOutboxReceivedItem<string>();

                logger.LogInformation($"RECEIVED EVENT from Azure Service at [{DateTime.Now}]:" +
                    $"{Environment.NewLine} - Subject: [{receivedItem.Subject}]" +
                    $"{Environment.NewLine} - UniqueIdentifier: [{receivedItem.UniqueIdentifier}]" +
                    $"{Environment.NewLine} - Content Type: [{receivedItem.ContentType}]" +
                    $"{Environment.NewLine} - Correlation ID: [{receivedItem.CorrelationId}]" +
                    $"{Environment.NewLine} - FIFO Grouping ID: [{receivedItem.FifoGroupingIdentifier}]" +
                    $"{Environment.NewLine} - Created Date UTC: [{receivedItem.PublishedItem.CreatedDateTimeUtc}]" +
                    $"{Environment.NewLine} - Scheduled Date UTC: [{receivedItem.PublishedItem.ScheduledPublishDateTimeUtc?.ToString() ?? "NOT Scheduled"}]" +
                    $"{Environment.NewLine} - Scheduled Date (Local): [{receivedItem.PublishedItem.ScheduledPublishDateTimeUtc?.ToLocalTime().ToString() ?? "NOT Scheduled"}]" +
                    $"{Environment.NewLine} - Publish Target: [{receivedItem.PublishedItem.PublishTarget}]" +
                    $"{Environment.NewLine} - Publish Attempts: [{receivedItem.PublishedItem.PublishAttempts}]" +
                    $"{Environment.NewLine} - Publish Status: [{receivedItem.PublishedItem.Status}]" +
                    $"{Environment.NewLine} - Payload:{Environment.NewLine}{receivedItem.ParsedBody}" +
                    Environment.NewLine
                );

                timer.Stop();
                logger.LogInformation($"Message Processing completed at [{DateTime.Now}] in [{timer.Elapsed.ToElapsedTimeDescriptiveFormat()}].");
            }
            catch (Exception exc)
            {
                timer.Stop();
                //Throw an Exception so that AzureFunctions will 'Abandon()' the service message and enable
                //  retrying of Delivery again (until MaxDeliveryCount limit is reached)!
                throw new Exception(
                    $"An unexpected error was encountered during processing of the Azure Service Bus Message -- " +
                    $" [ID={serviceBusMessage.MessageId}] [Subject={serviceBusMessage.Subject}] -- " +
                    $" after [{timer.Elapsed.ToElapsedTimeDescriptiveFormat()}] of processing time.",
                    exc
                );
            }

            return Task.CompletedTask;
        }
    }

}
