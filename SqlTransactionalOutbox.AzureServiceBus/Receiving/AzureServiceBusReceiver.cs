using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using SqlTransactionalOutbox.AzureServiceBus.Caching;
using SqlTransactionalOutbox.AzureServiceBus.Common;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.Receiving;

namespace SqlTransactionalOutbox.AzureServiceBus.Receiving
{
    public class AzureServiceBusReceiver<TUniqueIdentifier, TPayload> : BaseAzureServiceBusClient
    {
        public delegate Task ReceivedItemHandlerAsyncDelegate(
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

            this.AzureServiceBusConnection = InitAzureServiceBusConnection(azureServiceBusConnectionString);

            this.SubscriptionClientCache = new AzureSubscriptionClientCache();

            this.OutboxItemFactory = outboxItemFactory.AssertNotNull(nameof(outboxItemFactory));
        }

        protected ServiceBusConnection InitAzureServiceBusConnection(string azureServiceBusConnectionString)
        {
            azureServiceBusConnectionString.AssertNotNullOrWhiteSpace(nameof(azureServiceBusConnectionString));
            var connectionStringBuilder = new ServiceBusConnectionStringBuilder(azureServiceBusConnectionString);

            var options = this.Options;
            if (options.ClientOperationTimeout != null)
                connectionStringBuilder.OperationTimeout = (TimeSpan)options.ClientOperationTimeout;

            if (options.ConnectionIdleTimeout != null)
                connectionStringBuilder.ConnectionIdleTimeout = options.ConnectionIdleTimeout;

            var serviceBusConnection = new ServiceBusConnection(connectionStringBuilder.ToString());
            return serviceBusConnection;
        }

        public virtual UnregisterHandlerAsyncDelegate RegisterReceivedItemHandler(
            string topicPath,
            string subscriptionName,
            ISqlTransactionalOutboxReceivedItemHandler<TUniqueIdentifier, TPayload> receivedItemHandler,
            Func<IMessageSession, Message, Task> fifoSessionEndAsyncHandler = null
        )
        {
            //Validate handler and other references...
            receivedItemHandler.AssertNotNull(nameof(receivedItemHandler));

            //Our interface matches the Delegate signature as an alternative implementation (to help separate
            //  code responsibilities via class interface implementation...
            return RegisterMessageHandlerInternal(
                topicPath, 
                subscriptionName, 
                receivedItemHandler.HandleReceivedItemAsync,
                fifoSessionEndAsyncHandler: fifoSessionEndAsyncHandler
            );
        }

        public virtual UnregisterHandlerAsyncDelegate RegisterReceivedItemHandler(
            string topicPath,
            string subscriptionName,
            ReceivedItemHandlerAsyncDelegate receivedItemHandlerAsyncDelegateFunc,
            Func<IMessageSession, Message, Task> fifoSessionEndAsyncHandler = null
        )
        {
            return RegisterMessageHandlerInternal(
                topicPath, 
                subscriptionName, 
                receivedItemHandlerAsyncDelegateFunc, 
                fifoSessionEndAsyncHandler: fifoSessionEndAsyncHandler
            );
        }

        public delegate Task UnregisterHandlerAsyncDelegate(TimeSpan inflightWaitTimeSpan);

        protected virtual UnregisterHandlerAsyncDelegate RegisterMessageHandlerInternal(
            string topicPath,
            string subscriptionName,
            ReceivedItemHandlerAsyncDelegate receivedItemHandlerAsyncDelegateFunc,
            bool autoMessageFinalizationEnabled = true,
            Func<IMessageSession, Message, Task> fifoSessionEndAsyncHandler = null
        )
        {
            //Validate handler and other references...
            receivedItemHandlerAsyncDelegateFunc.AssertNotNull(nameof(receivedItemHandlerAsyncDelegateFunc));

            var subscriptionClient = GetSubscriptionClient(
                topicPath.AssertNotNullOrWhiteSpace(nameof(topicPath)),
                subscriptionName.AssertNotNullOrWhiteSpace(nameof(subscriptionName))
            );

            UnregisterHandlerAsyncDelegate unregisterHandlerAsyncFunc;

            //Complete full Handler Registration with provided options, etc...
            if (Options.FifoEnforcedReceivingEnabled)
            {
                subscriptionClient.RegisterSessionHandler(
                    handler: async (sessionClient, message, cancellationToken) =>
                    {
                        try
                        {
                            await ExecuteRegisteredMessageHandlerAsync(
                                //Note: We Must use the MessageSession as the Client for processing messages with the Locked Session!
                                (IReceiverClient) sessionClient,
                                message,
                                receivedItemHandlerAsyncDelegateFunc,
                                autoMessageFinalizationEnabled
                            ).ConfigureAwait(false);
                        }
                        finally
                        {
                            if (sessionClient?.IsClosedOrClosing == false)
                            {
                                if (fifoSessionEndAsyncHandler == null && Options.ReleaseSessionWhenNoHandlerIsProvided)
                                {
                                    await sessionClient.CloseAsync().ConfigureAwait(false);
                                }
                                else if (fifoSessionEndAsyncHandler != null)
                                {
                                    await fifoSessionEndAsyncHandler(sessionClient, message).ConfigureAwait(false);
                                }
                            }
                        }
                    },
                    sessionHandlerOptions: new SessionHandlerOptions(ExceptionReceivedHandlerAsync)
                    {
                        MaxConcurrentSessions = Options.MaxConcurrentReceiversOrSessions,
                        AutoComplete = false,
                        //Good Detail Summary of this property is at: https://stackoverflow.com/a/60381046/7293142
                        MaxAutoRenewDuration = this.Options.MaxAutoRenewDuration
                    }
                );

                //Initialize the SESSION un-register Delegate to provide easy removal of this Handler for re-use of hte Receiver Client!
                unregisterHandlerAsyncFunc = (inflightWaitTimeSpan) => subscriptionClient.UnregisterSessionHandlerAsync(inflightWaitTimeSpan);
            }
            else
            {
                subscriptionClient.RegisterMessageHandler(
                    handler: (message, cancellationToken) => ExecuteRegisteredMessageHandlerAsync(
                        subscriptionClient,
                        message,
                        receivedItemHandlerAsyncDelegateFunc, 
                        autoMessageFinalizationEnabled
                    ),
                    messageHandlerOptions: new MessageHandlerOptions(ExceptionReceivedHandlerAsync)
                    {
                        MaxConcurrentCalls = Options.MaxConcurrentReceiversOrSessions,
                        AutoComplete = false,
                        //Good Detail Summary of this property is at: https://stackoverflow.com/a/60381046/7293142
                        MaxAutoRenewDuration = this.Options.MaxAutoRenewDuration
                    }
                );

                //Initialize the Un-register Delegate to provide easy removal of this Handler for re-use of hte Receiver Client!
                unregisterHandlerAsyncFunc = (inflightWaitTimeSpan) => subscriptionClient.UnregisterMessageHandlerAsync(inflightWaitTimeSpan);
            }

            //Return the Delegate for Un-registering via Callback!
            return unregisterHandlerAsyncFunc;
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

            //ENSURE WE DISPOSE of the Queue when we are done!
            await using var dynamicReceiverQueue = CreateAsyncReceiverQueue(
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

            //A SHARED CONTEXT SPECIFIC reference to be set by the Registered Handler and
            //  Called Safely by the Disposal of the Receiver Queue when Disposed!
            IMessageSession queueContextSessionClient = null;
            
            //This reference enables deferred un-registration of the Handler when the Receiver Queue is Disposed!
            //NOTE: this is CRITICAl to allow the cached Receiver Client to be re-used...
            UnregisterHandlerAsyncDelegate unregisterHandlerFunc = null;

            //Initialize the producer/consumer queue for asynchronously & dynamically receiving items produced from the
            //  Azure Service Bus by being populated from our handler.
            var dynamicAsyncReceiverQueue = new SqlTransactionalOutboxReceiverQueue<TUniqueIdentifier, TPayload>(
                disposedCallbackAsyncHandler: async () =>
                {
                    if (queueContextSessionClient?.IsClosedOrClosing == false)
                    {
                        await queueContextSessionClient.CloseAsync().ConfigureAwait(false);
                    }

                    //WE MUST UNREGISTER the Handler as soon as we are done enumerating or else the Azure Service Bus Client
                    //  that is cached will throw Operation Exceptions when future processing attempts to register another Handler!
                    // ReSharper disable once AccessToModifiedClosure
                    if (unregisterHandlerFunc != null)
                    {
                        // ReSharper disable once AccessToModifiedClosure
                        await unregisterHandlerFunc.Invoke(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
                    }
                }
            );

            //Attempt to Receive & Handle the Messages just published for End-to-End Validation!
            unregisterHandlerFunc = this.RegisterMessageHandlerInternal(
                topicPath,
                subscriptionName,
                receivedItemHandlerAsyncDelegateFunc: async (outboxPublishedItem) =>
                {
                    //Set the unit test reference to ENABLE/NOTIFY for Continuation to complete the TEST!
                    //NOTE: We do NOT perform the Assert Tests inside this handler, because we need the framework
                    //      to acknowledge that the published item was successfully received and will handled
                    //      after the Test is able to proceed.
                    await dynamicAsyncReceiverQueue.AddAsync(outboxPublishedItem).ConfigureAwait(false);
                },
                autoMessageFinalizationEnabled: false,
                //Must specify a Session Handler so that it's not automatically closed on us...
                fifoSessionEndAsyncHandler: ((sessionClient, message) =>
                {
                    //SET but only need to set ONE TIME!
                    queueContextSessionClient ??= sessionClient;
                    return Task.CompletedTask;
                })
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
            IReceiverClient azureServiceBusClient,
            Message message,
            ReceivedItemHandlerAsyncDelegate receivedItemHandlerAsyncDelegateFunc,
            bool automaticFinalizationEnabled = true
        )
        {
            azureServiceBusClient.AssertNotNull(nameof(azureServiceBusClient));
            message.AssertNotNull(nameof(message));
            receivedItemHandlerAsyncDelegateFunc.AssertNotNull(nameof(receivedItemHandlerAsyncDelegateFunc));

            AzureServiceBusReceivedItem<TUniqueIdentifier, TPayload> azureServiceBusReceivedItem = null;

            //FIRST attempt to convert the MEssage into an AzureServiceBus Received Item....
            try
            {
                //Initialize the MessageHandler facade for processing the Azure Service Bus message...
                azureServiceBusReceivedItem = CreateReceivedItem(
                    message,
                    azureServiceBusClient,
                    Options.FifoEnforcedReceivingEnabled
                ).AssertNotNull(nameof(azureServiceBusReceivedItem));
            }
            catch (Exception exc)
            {
                var messageException = new Exception("The message could not be correctly initialized due to unexpected exception." +
                                                     " It must be Dead lettered to prevent blocking of the Service Bus as it will never be" +
                                                     " processed successfully.", exc);

                this.Options.LogErrorCallback?.Invoke(messageException);

                //An Exception happened during Message Initialization so we release and dead-letter this message because it can't be initialized!
                await azureServiceBusClient.DeadLetterAsync(message.SystemProperties.LockToken);
            }

            //IF the Received Item is successfully initialized then process it!
            if (azureServiceBusReceivedItem != null)
            {
                try
                {
                    //Execute the delegate to handle/process the published item...
                    await receivedItemHandlerAsyncDelegateFunc(azureServiceBusReceivedItem).ConfigureAwait(false);

                    //If necessary, we need to Finalize the item with Azure Service Bus (Complete/Abandon) based on the Status returned on the item!
                    if (automaticFinalizationEnabled && !azureServiceBusReceivedItem.IsStatusFinalized)
                    {
                        await azureServiceBusReceivedItem.SendFinalizedStatusToAzureServiceBusAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception exc)
                {
                    this.Options.LogErrorCallback?.Invoke(exc);

                    //Always attempt to Reject/Abandon the message if any unhandled exceptions are thrown...
                    if (azureServiceBusReceivedItem?.IsStatusFinalized == false)
                    {
                        //Finalize the status as set by the Delegate function if it's not already Finalized!
                        //NOTE: We ALWAYS do this even if autoMessageFinalizationEnabled is false to prevent BLOCKING...
                        await azureServiceBusReceivedItem.SendFinalizedStatusToAzureServiceBusAsync().ConfigureAwait(false);
                    }
                }
            }
        }

        protected virtual ISubscriptionClient CreateNewAzureServiceBusReceiverClient(string topicPath, string subscriptionName)
        {
            var newSubscriptionClient = new SubscriptionClient(
                AzureServiceBusConnection,
                topicPath.AssertNotNullOrWhiteSpace(nameof(topicPath)),
                subscriptionName.AssertNotNullOrWhiteSpace(nameof(subscriptionName)),
                ReceiveMode.PeekLock,
                //Use RetryPolicy provided by Options!
                this.Options.RetryPolicy ?? RetryPolicy.Default
            );

            if (this.Options.PrefetchCount > 0)
            {
                newSubscriptionClient.PrefetchCount = this.Options.PrefetchCount;
            }

            //Configure additional options...

            //Enable dynamic configuration if specified...
            this.ClientConfigurationFunc?.Invoke(newSubscriptionClient);

            return newSubscriptionClient;
        }

        protected virtual AzureServiceBusReceivedItem<TUniqueIdentifier, TPayload> CreateReceivedItem(
            Message message,
            IReceiverClient azureServiceBusClient,
            bool isFifoProcessingEnabled
        )
        {
            var azureServiceBusReceivedItem = new AzureServiceBusReceivedItem<TUniqueIdentifier, TPayload>(
                azureServiceBusMessage: message,
                outboxItemFactory: this.OutboxItemFactory,
                azureServiceBusClient: azureServiceBusClient,
                isFifoProcessingEnabled: isFifoProcessingEnabled
            );

            return azureServiceBusReceivedItem;
        }

        protected virtual Task ExceptionReceivedHandlerAsync(ExceptionReceivedEventArgs args)
        {
            var context = args.ExceptionReceivedContext;

            var message = $"An unexpected exception occurred while attempting to receive messages from Azure Service Bus;" +
                          $" [Endpoint={context.Endpoint}], [EntityPath={context.EntityPath}], [ExecutingAction={context.Action}]";

            var logException = new Exception(message, args.Exception);

            //Throw the exception if we can't Log it...
            if(this.Options.LogErrorCallback == null)
                Debug.WriteLine(logException.GetMessagesRecursively());
            else
                this.Options.LogErrorCallback(logException);

            return Task.CompletedTask;
        }

    }
}
