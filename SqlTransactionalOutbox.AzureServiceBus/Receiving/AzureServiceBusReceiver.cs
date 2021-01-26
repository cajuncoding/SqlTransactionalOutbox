using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.ServiceBus.Primitives;
using Newtonsoft.Json;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.Interfaces;
using SqlTransactionalOutbox.Publishing;
using SqlTransactionalOutbox.Receiving;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class AzureServiceBusReceiver<TUniqueIdentifier, TPayload>
    {
        public delegate Task ReceivedItemHandlerAsync(
            ISqlTransactionalOutboxPublishedItem<TUniqueIdentifier, TPayload> outboxPublishedItem
        );

        protected ServiceBusConnection AzureServiceBusConnection { get; }

        protected AzureSubscriptionClientCache SubscriptionClientCache { get; }

        protected ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> OutboxItemFactory { get; }

        public bool IsFifoEnforcedReceivingEnabled { get; }

        public AzureServiceBusReceiver(
            string azureServiceBusConnectionString,
            ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> outboxItemFactory,
            bool enableFifoEnforcedReceiving = false
        )
        {
            this.AzureServiceBusConnection = new ServiceBusConnection(
                azureServiceBusConnectionString.AssertNotNullOrWhiteSpace(nameof(azureServiceBusConnectionString))
            );

            this.SubscriptionClientCache = new AzureSubscriptionClientCache();

            this.OutboxItemFactory = outboxItemFactory.AssertNotNull(nameof(outboxItemFactory));

            this.IsFifoEnforcedReceivingEnabled = enableFifoEnforcedReceiving;
        }

        public virtual void RegisterPublishedOutboxItemHandler(
            string topicPath,
            string subscriptionName,
            ISqlTransactionalOutboxReceivedItemHandler<TUniqueIdentifier, TPayload> receivedItemHandler
            //TODO: Add Options for dynamic configuration of RetryPolicy, MaxAutoRenewDuration, etc.
        )
        {
            //Validate handler and other references...
            receivedItemHandler.AssertNotNull(nameof(receivedItemHandler));

            //Our interface matches the Delegate signature as an alternative implementation (to help separate
            //  code responsibilities via class interface implementation...
            RegisterMessageHandlerInternal(topicPath, subscriptionName, receivedItemHandler.HandleReceivedItemAsync);
        }

        public virtual void RegisterPublishedOutboxItemHandler(
            string topicPath,
            string subscriptionName,
            ReceivedItemHandlerAsync receivedItemHandlerAsyncFunc
            //TODO: Add Options for dynamic configuration of RetryPolicy, MaxAutoRenewDuration, etc.
        )
        {
            RegisterMessageHandlerInternal(topicPath, subscriptionName, receivedItemHandlerAsyncFunc);
        }

        protected virtual void RegisterMessageHandlerInternal(
            string topicPath,
            string subscriptionName,
            ReceivedItemHandlerAsync receivedItemHandlerAsyncFunc,
            //TODO: Add Options for dynamic configuration of RetryPolicy, MaxAutoRenewDuration, etc.
            bool automaticFinalizationEnabled = true
        )
        {
            //Validate handler and other references...
            receivedItemHandlerAsyncFunc.AssertNotNull(nameof(receivedItemHandlerAsyncFunc));

            var subscriptionClient = GetSubscriptionClient(
                topicPath.AssertNotNullOrWhiteSpace(nameof(topicPath)),
                subscriptionName.AssertNotNullOrWhiteSpace(nameof(subscriptionName))
            );

            //Complete full Handler Registration with provided options, etc...
            if (IsFifoEnforcedReceivingEnabled)
            {
                subscriptionClient.RegisterSessionHandler(
                    handler: (session, message, cancellationToken) => ExecuteRegisteredMessageHandlerAsync(message, (IReceiverClient)session, receivedItemHandlerAsyncFunc, automaticFinalizationEnabled),
                    sessionHandlerOptions: new SessionHandlerOptions(ExceptionReceivedHandlerAsync)
                    {
                        MaxConcurrentSessions = 1,
                        AutoComplete = false,
                        //TODO: Add Support for Configuring MaxAutoRenewDuration via options...
                        //Good Detail Summary of this property is at: https://stackoverflow.com/a/60381046/7293142
                        MaxAutoRenewDuration = TimeSpan.FromMinutes(10)
                    }
                );
            }
            else
            {
                subscriptionClient.RegisterMessageHandler(
                    handler: (message, cancellationToken) => ExecuteRegisteredMessageHandlerAsync(message, subscriptionClient, receivedItemHandlerAsyncFunc, automaticFinalizationEnabled),
                    messageHandlerOptions: new MessageHandlerOptions(ExceptionReceivedHandlerAsync)
                    {
                        MaxConcurrentCalls = 1,
                        AutoComplete = false,
                        //TODO: Add Support for Configuring MaxAutoRenewDuration via options...
                        //Good Detail Summary of this property is at: https://stackoverflow.com/a/60381046/7293142
                        MaxAutoRenewDuration = TimeSpan.FromMinutes(10)
                    }
                );
            }

        }

        public virtual async IAsyncEnumerable<ISqlTransactionalOutboxPublishedItem<TUniqueIdentifier, TPayload>> RetrieveAsyncEnumerable(
            string topicPath,
            string subscriptionName,
            TimeSpan receiveWaitTimeout
        )
        {
            var cancellationSource = new CancellationTokenSource(receiveWaitTimeout);
            await foreach (var item in RetrieveAsyncEnumerable(topicPath, subscriptionName, cancellationSource.Token))
            {
                yield return item;
            }

            //Flag completion & Ensure any other background/async processes are fully cancelled!
            cancellationSource.Cancel();
        }

        public virtual async IAsyncEnumerable<ISqlTransactionalOutboxPublishedItem<TUniqueIdentifier, TPayload>> RetrieveAsyncEnumerable(
            string topicPath,
            string subscriptionName,
            [EnumeratorCancellation] CancellationToken cancellationToken = default
        )
        {
            var dynamicReceiverQueue = CreateAsyncReceiverQueue(
                topicPath.AssertNotNullOrWhiteSpace(nameof(topicPath)),
                subscriptionName.AssertNotNullOrWhiteSpace(nameof(subscriptionName))
            );

            await foreach (var item in dynamicReceiverQueue.AsEnumerableAsync(cancellationToken))
            {
                yield return item;
            }
        }

        /// <summary>
        /// Initialize a producer/consumer queue for asynchronously & dynamically receiving items produced from the
        ///  Azure Service Bus.  Note, using this queue means that ALL Items will automatically be acknowledged as
        ///  as successfully received with no ability to Reject/Abandon them.
        /// </summary>
        /// <param name="topicPath"></param>
        /// <param name="subscriptionName"></param>
        public virtual SqlTransactionalOutboxReceiverQueue<TUniqueIdentifier, TPayload> CreateAsyncReceiverQueue(
            string topicPath,
            string subscriptionName
            //TODO: Add Options for dynamic configuration of RetryPolicy, MaxAutoRenewDuration, etc.
        )
        {
            topicPath.AssertNotNullOrWhiteSpace(nameof(topicPath));
            subscriptionName.AssertNotNullOrWhiteSpace(nameof(subscriptionName));

            //Initialize the producer/consumer queue for asynchronously & dynamically receiving items produced from the
            //  Azure Service Bus by being populated from our handler.
            SqlTransactionalOutboxReceiverQueue<TUniqueIdentifier, TPayload> dynamicAsyncReceiverQueue 
                = new SqlTransactionalOutboxReceiverQueue<TUniqueIdentifier, TPayload>();

            //Attempt to Receive & Handle the Messages just published for End-to-End Validation!
            this.RegisterMessageHandlerInternal(
                topicPath,
                subscriptionName,
                receivedItemHandlerAsyncFunc: async (outboxPublishedItem) =>
                {
                    //Set the unit test reference to ENABLE/NOTIFY for Continuation to complete the TEST!
                    //NOTE: We do NOT perform the Assert Tests inside this handler, because we need the framework
                    //      to acknowledge that the published item was successfully received and will handled
                    //      after the Test is able to proceed.
                    await dynamicAsyncReceiverQueue.AddAsync(outboxPublishedItem).ConfigureAwait(false);
                },
                automaticFinalizationEnabled: false
            );

            return dynamicAsyncReceiverQueue;
        }

        protected virtual ISubscriptionClient GetSubscriptionClient(
            string topicPath,
            string subscriptionName
        )
        {
            var subscriptionClient = SubscriptionClientCache.InitializeClient(
                topicPath.AssertNotNullOrWhiteSpace(nameof(topicPath)),
                subscriptionName.AssertNotNullOrWhiteSpace(nameof(subscriptionName)),
                newClientFactory:() => CreateNewAzureServiceBusClient(topicPath, subscriptionName)
            );

            return subscriptionClient;
        }

        protected virtual async Task ExecuteRegisteredMessageHandlerAsync(
            Message message,
            IReceiverClient azureServiceBusClient,
            ReceivedItemHandlerAsync receivedItemHandlerAsyncFunc,
            bool automaticFinalizationEnabled = true
        )
        {
            //Initialize the MessageHandler facade for processing the Azure Service Bus message...
            var messageHandler = CreateMessageHandler(message, azureServiceBusClient);

            //Initialize the Received Item wrapper for Azure Service Bus!
            //NOTE: Always ensure we properly dispose of the ISqlTransactionalOutboxReceivedItem<> after processing!
            var outboxReceivedItem = messageHandler.CreateOutboxPublishedItem();

            try
            {
                //Execute the delegate to handle/process the published item...
                await receivedItemHandlerAsyncFunc(outboxReceivedItem).ConfigureAwait(false);

                //If necessary, we need to Finalize the item with Azure Service Bus (Complete/Abandon) based on the Status returned on the item!
                if(automaticFinalizationEnabled && !outboxReceivedItem.IsStatusFinalized)
                    await messageHandler.SendFinalizedStatusToAzureServiceBusAsync(outboxReceivedItem.Status).ConfigureAwait(false);
            }
            catch (Exception)
            {
                //TODO: Add Logging of the Exception...

                //Always Reject/Abandon the message if any unhandled exceptions are thrown...
                //NOTE: We ALWAYS do this even if automaticFinalizationEnabled is false...
                if (!outboxReceivedItem.IsStatusFinalized)
                    await messageHandler.SendFinalizedStatusToAzureServiceBusAsync(OutboxReceivedItemProcessingStatus.RejectAndAbandon);
            }
        }

        protected virtual ISubscriptionClient CreateNewAzureServiceBusClient(string topicPath, string subscriptionName)
        {
            //TODO: Add Default Exponential Retry Policy as default!
            var newSubscriptionClient = new SubscriptionClient(
                AzureServiceBusConnection,
                topicPath,
                subscriptionName,
                ReceiveMode.PeekLock,
                //TODO: Implement support for custom Retry Policies...
                RetryPolicy.Default
            );

            return newSubscriptionClient;
        }

        protected virtual AzureServiceBusMessageHandler<TUniqueIdentifier, TPayload> CreateMessageHandler(
            Message message,
            IReceiverClient azureServiceBusClient
        )
        {
            //Initialize the Message Handler which provides capabilities for mapping
            // the Azure Message to OutBox interfaces, and facade methods to simplify working
            //  Acknowledging/Rejecting the message on the service bus.
            var messageHandler = new AzureServiceBusMessageHandler<TUniqueIdentifier, TPayload>(
                message,
                azureServiceBusClient,
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
