using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Newtonsoft.Json;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.Interfaces;
using SqlTransactionalOutbox.Publishing;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class AzureServiceBusReceiver<TUniqueIdentifier, TPayload>
    {
        protected ServiceBusConnection AzureServiceBusConnection { get; }

        protected AzureReceiverClientCache ReceiverClientCache { get; }

        protected ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> OutboxItemFactory { get; }

        public AzureServiceBusReceiver(
            string azureServiceBusConnectionString,
            ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> outboxItemFactory
        )
        {
            this.AzureServiceBusConnection = new ServiceBusConnection(
                azureServiceBusConnectionString.AssertNotNullOrWhiteSpace(nameof(azureServiceBusConnectionString))
            );

            this.ReceiverClientCache = new AzureReceiverClientCache();

            this.OutboxItemFactory = outboxItemFactory.AssertNotNull(nameof(outboxItemFactory));
        }

        public virtual IReceiverClient RegisterReceiverHandler(
            string topicPath,
            string subscriptionName,
            Func<ISqlTransactionalOutboxReceivedMessage<TUniqueIdentifier, TPayload>, Task<OutboxReceivedItemProcessingStatus>> receivedItemHandlerAsyncFunc
            //TODO: Add Options for dynamic configuration of RetryPolicy, MaxAutoRenewDuration, etc.
        )
        {
            //Validate handler and other references...
            receivedItemHandlerAsyncFunc.AssertNotNull(nameof(receivedItemHandlerAsyncFunc));

            var receiverClient = GetReceiverClient(
                topicPath.AssertNotNullOrWhiteSpace(nameof(topicPath)),
                subscriptionName.AssertNotNullOrWhiteSpace(nameof(subscriptionName))
            );

            //Complete full Handler Registration with provided options, etc...
            receiverClient.RegisterMessageHandler(
                async (message, cancellationToken) =>
                {
                    //Initialize the MessageHandler facade for processing the Azure Service Bus message...
                    var messageHandler = CreateMessageHandler(message, receiverClient);

                    //By default we will Reject/Abandon as fallback if any exceptions are raised...
                    try
                    {
                        //Initialize the Received Item wrapper for Azure Service Bus!
                        //NOTE: Always ensure we properly dispose of the ISqlTransactionalOutboxReceivedItem<> after processing!
                        var outboxReceivedItem = messageHandler.CreateOutboxReceivedItem();

                        var processingStatus = await receivedItemHandlerAsyncFunc(outboxReceivedItem).ConfigureAwait(false);

                        //Notify Azure Service Bus to Complete the item or to Abandon as defined by the status returned!
                        switch(processingStatus)
                        {
                            case OutboxReceivedItemProcessingStatus.AcknowledgeSuccessfulReceipt:
                                await messageHandler.AcknowledgeSuccessfulReceiptAsync();
                                break;
                            case OutboxReceivedItemProcessingStatus.RejectAndAbandon:
                            default:
                                await messageHandler.RejectReceiptAndAbandonAsync();
                                break;
                        }
                    }
                    catch (Exception)
                    {
                        //Always Reject/Abandon the message if any unhandled exceptions are thrown...
                        await messageHandler.RejectReceiptAndAbandonAsync();
                        //TODO: Add Logging of the Exception...
                    }
                },
                new MessageHandlerOptions(ExceptionReceivedHandlerAsync)
                {
                    MaxConcurrentCalls = 1,
                    AutoComplete = false,
                    //TODO: Add Support for Configuring MaxAutoRenewDuration...
                    MaxAutoRenewDuration = TimeSpan.FromSeconds(300)

                }
            );

            return receiverClient;
        }

        protected virtual IReceiverClient GetReceiverClient(
            string topicPath,
            string subscriptionName
        )
        {
            var receiverClient = ReceiverClientCache.InitializeReceiverClient(
                topicPath.AssertNotNullOrWhiteSpace(nameof(topicPath)),
                subscriptionName.AssertNotNullOrWhiteSpace(nameof(subscriptionName)),
                newReceiverClientFactory:() => CreateNewReceiverClient(topicPath, subscriptionName)
            );

            return receiverClient;
        }

        protected virtual IReceiverClient CreateNewReceiverClient(string topicPath, string subscriptionName)
        {
            //TODO: Add Default Exponential Retry Policy as default!
            var newReceiverClient = new SubscriptionClient(
                AzureServiceBusConnection,
                topicPath,
                subscriptionName,
                ReceiveMode.PeekLock,
                //TODO: Implement support for custom Retry Policies...
                RetryPolicy.Default
            );

            return newReceiverClient;
        }

        protected virtual AzureServiceBusMessageHandler<TUniqueIdentifier, TPayload> CreateMessageHandler(
            Message message, 
            IReceiverClient receiverClient
        )
        {
            //Initialize the Message Handler which provides capabilities for mapping
            // the Azure Message to OutBox interfaces, and facade methods to simplify working
            //  Acknowledging/Rejecting the message on the service bus.
            var messageHandler = new AzureServiceBusMessageHandler<TUniqueIdentifier, TPayload>(
                message,
                receiverClient,
                this.OutboxItemFactory
            );
         
            return messageHandler;
        }

        protected virtual Task ExceptionReceivedHandlerAsync(ExceptionReceivedEventArgs args)
        {
            var context = args.ExceptionReceivedContext;

            var message = $"An unexpected exception occurred while attempting to receive messages from Azure Service Bus;" +
                          $" [Endpoint={context.Endpoint}], [EntityPath={context.EntityPath}], [ExecutingAction={context.Action}]";

            //TODO: Implement actual Logging here via Options Logging Provider...
            //_logger.LogError(exceptionReceivedEventArgs.Exception, $"Endpoint: {context.Endpoint} | Entity Path: {context.EntityPath} | Executing Action: {context.Action}");
            throw new Exception(message, args.Exception);
        }
    }
}
