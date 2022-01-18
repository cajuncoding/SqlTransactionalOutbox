using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlTransactionalOutbox.AzureServiceBus;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.SampleApp.AzureFunctions;

namespace SqlTransactionalOutbox.SampleApp.ConsoleApp
{
    /// <summary>
    ///******************************************************************************************
    /// 3. RECEIVING Messages that were Published via AzureServiceBus
    ///
    /// We need a Message Receiver to pickup the Messages that are Published!
    ///******************************************************************************************
    /// </summary>
    /// <typeparam name="TPayload"></typeparam>
    public class OutboxFifoReceiver<TPayload> : IAsyncDisposable
    {
        // NOTE: Since our Subscription is Session based we must enable Fifo processing or errors will occur!
        protected DefaultFifoAzureServiceBusReceiver<TPayload> ServiceBusFifoReceiver { get; set; }

        public OutboxFifoReceiver(SampleAppConfig configSettings)
        {
            //We need a Message Receiver to pickup the Messages that are Published!
            // NOTE: Since our payloads are simple strings we use 'string' payload type!
            ServiceBusFifoReceiver = new DefaultFifoAzureServiceBusReceiver<TPayload>(
                configSettings.AzureServiceBusConnectionString, 
                configSettings.AzureServiceBusTopic, 
                configSettings.AzureServiceBusSubscription
            );
        }

        public async Task StartReceivingWithAsync(Action<string> consoleLogAction, CancellationToken cancellationToken = default)
        {
            consoleLogAction.AssertNotNull(nameof(consoleLogAction));

            //RUN the Receiver!
            await ServiceBusFifoReceiver.StartReceivingAsync((receivedItem) =>
            {
                //Since we get an OutboxItem injected we can just access the Parsed Payload...
                //NOTE: If you used the RAW ServiceBus to receive the message then the Extension methods
                //      can be used to easily convert from the ServiceBusReceivedMessage to a parsed OutboxItem; 
                //      see the Azure Functions Sample App for how this is easily done in 
                //Console.WriteLine($"[{nameof(OutboxFifoReceiver<string>)}] MESSAGE RECEIVED: {outboxItem.PublishedItem.CreatedDateTimeUtc.}: {message}");
                consoleLogAction(
                    $@"[{nameof(OutboxFifoReceiver<string>)}] MESSAGE RECEIVED at [{DateTimeOffset.Now}]:" +
                    //$"{Environment.NewLine} - Subject: [{receivedItem.Subject}]" +
                    //$"{Environment.NewLine} - UniqueIdentifier: [{receivedItem.UniqueIdentifier}]" +
                    //$"{Environment.NewLine} - Content Type: [{receivedItem.ContentType}]" +
                    //$"{Environment.NewLine} - Correlation ID: [{receivedItem.CorrelationId}]" +
                    //$"{Environment.NewLine} - FIFO Grouping ID: [{receivedItem.FifoGroupingIdentifier}]" +
                    $"{Environment.NewLine} - Created Date UTC: [{receivedItem.PublishedItem.CreatedDateTimeUtc}]" +
                    //$"{Environment.NewLine} - Publish Target: [{receivedItem.PublishedItem.PublishTarget}]" +
                    //$"{Environment.NewLine} - Publish Attempts: [{receivedItem.PublishedItem.PublishAttempts}]" +
                    //$"{Environment.NewLine} - Publish Status: [{receivedItem.PublishedItem.Status}]" +
                    $"{Environment.NewLine} - Payload Message: {receivedItem.ParsedBody}" +
                    Environment.NewLine
                );

                //Acknowledge that we have successfully finished working with the Received Item!
                receivedItem.AcknowledgeSuccessfulReceiptAsync();

                return Task.CompletedTask;
            }, cancellationToken);
        }

        public async Task StopReceivingAsync()
        {
            await ServiceBusFifoReceiver.StopReceivingAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await this.StopReceivingAsync();
            await ServiceBusFifoReceiver.DisposeAsync();
        }
    }
}
