using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using SqlTransactionalOutbox.AzureServiceBus.Common;
using SqlTransactionalOutbox.AzureServiceBus.Receiving;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.Receiving;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class AzureServiceBusReceiver<TUniqueIdentifier, TPayload> : BaseAzureServiceBusClient
    {
        public delegate Task ReceivedItemHandlerAsync(
            ISqlTransactionalOutboxReceivedItem<TUniqueIdentifier, TPayload> outboxReceivedItem
        );

        public AzureServiceBusReceivingOptions Options { get; }

        protected ServiceBusConnection AzureServiceBusConnection { get; }

        protected AzureSubscriptionClientCache SubscriptionClientCache { get; }

        protected ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> OutboxItemFactory { get; }

        public AzureServiceBusReceiver(
            string azureServiceBusConnectionString,
            ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> outboxItemFactory,
            AzureServiceBusReceivingOptions options = null
        )
        {
            this.Options = options ?? new AzureServiceBusReceivingOptions();

            this.AzureServiceBusConnection = new ServiceBusConnection(
                azureServiceBusConnectionString.AssertNotNullOrWhiteSpace(nameof(azureServiceBusConnectionString))
            );

            this.SubscriptionClientCache = new AzureSubscriptionClientCache();

            this.OutboxItemFactory = outboxItemFactory.AssertNotNull(nameof(outboxItemFactory));
        }

        public virtual void RegisterReceivedItemHandler(
            string topicPath,
            string subscriptionName,
            ISqlTransactionalOutboxReceivedItemHandler<TUniqueIdentifier, TPayload> receivedItemHandler
        )
        {
            //Validate handler and other references...
            receivedItemHandler.AssertNotNull(nameof(receivedItemHandler));

            //Our interface matches the Delegate signature as an alternative implementation (to help separate
            //  code responsibilities via class interface implementation...
            RegisterMessageHandlerInternal(topicPath, subscriptionName, receivedItemHandler.HandleReceivedItemAsync);
        }

        public virtual void RegisterReceivedItemHandler(
            string topicPath,
            string subscriptionName,
            ReceivedItemHandlerAsync receivedItemHandlerAsyncFunc
        )
        {
            RegisterMessageHandlerInternal(topicPath, subscriptionName, receivedItemHandlerAsyncFunc);
        }

        protected virtual void RegisterMessageHandlerInternal(
            string topicPath,
            string subscriptionName,
            ReceivedItemHandlerAsync receivedItemHandlerAsyncFunc,
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
            if (Options.FifoEnforcedReceivingEnabled)
            {
                subscriptionClient.RegisterSessionHandler(
                    handler: (session, message, cancellationToken) => ExecuteRegisteredMessageHandlerAsync(message, (IReceiverClient)session, receivedItemHandlerAsyncFunc, automaticFinalizationEnabled),
                    sessionHandlerOptions: new SessionHandlerOptions(ExceptionReceivedHandlerAsync)
                    {
                        MaxConcurrentSessions = 1,
                        AutoComplete = false,
                        //Good Detail Summary of this property is at: https://stackoverflow.com/a/60381046/7293142
                        MaxAutoRenewDuration = this.Options.MaxAutoRenewDuration
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
                        //Good Detail Summary of this property is at: https://stackoverflow.com/a/60381046/7293142
                        MaxAutoRenewDuration = this.Options.MaxAutoRenewDuration
                    }
                );
            }

        }

        public virtual async IAsyncEnumerable<ISqlTransactionalOutboxReceivedItem<TUniqueIdentifier, TPayload>> RetrieveAsyncEnumerable(
            string topicPath,
            string subscriptionName,
            TimeSpan receiveWaitPerItemTimeout,
            bool throwExceptionOnCancellation = false
        )
        {
            if (receiveWaitPerItemTimeout == default)
            {
                throw new ArgumentException("Wait time must be specified.", nameof(receiveWaitPerItemTimeout));
            }

            var dynamicReceiverQueue = CreateAsyncReceiverQueue(
                topicPath.AssertNotNullOrWhiteSpace(nameof(topicPath)),
                subscriptionName.AssertNotNullOrWhiteSpace(nameof(subscriptionName))
            );

            //Wait for Initial Data!
            ISqlTransactionalOutboxReceivedItem<TUniqueIdentifier, TPayload> item = null;
            do
            {
                var cancellationSource = new CancellationTokenSource(receiveWaitPerItemTimeout);

                try
                {
                    item = await dynamicReceiverQueue.TakeAsync(cancellationSource.Token);
                }
                catch (OperationCanceledException cancelExc) when (cancelExc.CancellationToken == cancellationSource.Token)
                {
                    //DO NOTHING as our operation should cancel gracefully unless otherwise specified!
                    if (throwExceptionOnCancellation)
                        throw;
                }

                //Return the item IF we have one...
                if (item != null)
                    yield return item;

            } while (item != null);

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
        )
        {
            topicPath.AssertNotNullOrWhiteSpace(nameof(topicPath));
            subscriptionName.AssertNotNullOrWhiteSpace(nameof(subscriptionName));

            //Initialize the producer/consumer queue for asynchronously & dynamically receiving items produced from the
            //  Azure Service Bus by being populated from our handler.
            var dynamicAsyncReceiverQueue = new SqlTransactionalOutboxReceiverQueue<TUniqueIdentifier, TPayload>();

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
                newClientFactory:() => CreateNewAzureServiceBusReceiverClient(topicPath, subscriptionName)
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
            var outboxReceivedItem = messageHandler.CreateReceivedOutboxItem();

            try
            {
                //Execute the delegate to handle/process the published item...
                await receivedItemHandlerAsyncFunc(outboxReceivedItem).ConfigureAwait(false);

                //If necessary, we need to Finalize the item with Azure Service Bus (Complete/Abandon) based on the Status returned on the item!
                if(automaticFinalizationEnabled && !outboxReceivedItem.IsStatusFinalized)
                    await messageHandler.SendFinalizedStatusToAzureServiceBusAsync(outboxReceivedItem.Status).ConfigureAwait(false);
            }
            catch (Exception exc)
            {
                this.Options.LogErrorCallback?.Invoke(exc);

                //Always Reject/Abandon the message if any unhandled exceptions are thrown...
                //NOTE: We ALWAYS do this even if automaticFinalizationEnabled is false...
                if (!outboxReceivedItem.IsStatusFinalized)
                    await messageHandler.SendFinalizedStatusToAzureServiceBusAsync(OutboxReceivedItemProcessingStatus.RejectAndAbandon);
            }
        }

        protected virtual ISubscriptionClient CreateNewAzureServiceBusReceiverClient(string topicPath, string subscriptionName)
        {
            var newSubscriptionClient = new SubscriptionClient(
                AzureServiceBusConnection,
                topicPath,
                subscriptionName,
                ReceiveMode.PeekLock,
                //Use RetryPolicy provided by Options!
                this.Options.RetryPolicy
            );

            //Enable dynamic configuration if specified...
            this.ClientConfigurationFunc?.Invoke(newSubscriptionClient);

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

            var logException = new Exception(message, args.Exception);

            //Throw the exception if we can't Log it...
            if(this.Options.LogErrorCallback == null)
                throw logException;

            this.Options.LogErrorCallback(logException);

            return Task.CompletedTask;
        }

    }
}
