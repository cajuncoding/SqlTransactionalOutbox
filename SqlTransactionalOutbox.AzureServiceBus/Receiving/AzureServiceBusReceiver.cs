using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using SqlTransactionalOutbox.AzureServiceBus.Common;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.Receiving;

namespace SqlTransactionalOutbox.AzureServiceBus.Receiving
{
    public class AzureServiceBusReceiver<TUniqueIdentifier, TPayload> : BaseAzureServiceBusClient, IAsyncDisposable
    {
        public delegate Task ReceivedItemHandlerAsyncDelegate(ISqlTransactionalOutboxReceivedItem<TUniqueIdentifier, TPayload> outboxReceivedItem);

        public AzureServiceBusReceivingOptions Options { get; }

        protected ServiceBusSessionProcessor ServiceBusSessionProcessor { get; set; } = null;
        protected ServiceBusProcessor ServiceBusProcessor { get; set; } = null;
        protected bool IsProcessing { get; set; } = false;

        protected ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> OutboxItemFactory { get; }

        public AzureServiceBusReceiver(
            string azureServiceBusConnectionString,
            ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> outboxItemFactory,
            AzureServiceBusReceivingOptions options = null
        ) : this(InitAzureServiceBusConnection(azureServiceBusConnectionString, options), outboxItemFactory, options)
        {
            DisposingEnabled = true;
        }

        public AzureServiceBusReceiver(
            ServiceBusClient azureServiceBusClient,
            ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> outboxItemFactory,
            AzureServiceBusReceivingOptions options = null
        )
        {
            this.Options = options ?? new AzureServiceBusReceivingOptions();
            this.AzureServiceBusClient = azureServiceBusClient.AssertNotNull(nameof(azureServiceBusClient));
            this.OutboxItemFactory = outboxItemFactory.AssertNotNull(nameof(outboxItemFactory));
        }

        public virtual Task StartReceivingAsync(
            string topicPath,
            string subscriptionName,
            ISqlTransactionalOutboxReceivedItemHandler<TUniqueIdentifier, TPayload> receivedItemHandler,
            CancellationToken cancellationToken = default
        )
        {
            if (!this.IsProcessing)
            {
                receivedItemHandler.AssertNotNull(nameof(receivedItemHandler));

                return StartReceivingInternalAsync(
                    topicPath,
                    subscriptionName,
                    receivedItemHandler.HandleReceivedItemAsync,
                    cancellationToken: cancellationToken
                );
            }

            return Task.CompletedTask;
        }

        public virtual Task StartReceivingAsync(
            string topicPath,
            string subscriptionName,
            ReceivedItemHandlerAsyncDelegate receivedItemHandlerAsyncDelegateFunc,
            CancellationToken cancellationToken = default
        )
        {
            if (!this.IsProcessing)
            {
                return StartReceivingInternalAsync(
                    topicPath,
                    subscriptionName,
                    receivedItemHandlerAsyncDelegateFunc,
                    cancellationToken: cancellationToken
                );
            }
            
            return Task.CompletedTask;
        }

        public virtual async Task StopReceivingAsync(Func<ProcessSessionEventArgs, Task> fifoSessionEndAsyncHandler = null)
        {
            //Always update the Processing Flag
            this.IsProcessing = false;

            //Close down FIFO Session Processor...
            if (this.ServiceBusSessionProcessor != null)
            {
                try
                {
                    if (this.ServiceBusSessionProcessor.IsProcessing)
                        await this.ServiceBusSessionProcessor.StopProcessingAsync();

                    if (!this.ServiceBusSessionProcessor.IsClosed)
                    {
                        if (fifoSessionEndAsyncHandler != null)
                            ServiceBusSessionProcessor.SessionClosingAsync += fifoSessionEndAsyncHandler;

                        await this.ServiceBusSessionProcessor.CloseAsync();
                    }
                }
                finally
                {
                    await this.ServiceBusSessionProcessor.DisposeAsync();
                    this.ServiceBusSessionProcessor = null;
                }
            }

            //Close down  normal Processor...
            if (this.ServiceBusProcessor != null)
            {
                try
                {
                    if (this.ServiceBusProcessor.IsProcessing)
                        await this.ServiceBusProcessor.StopProcessingAsync();

                    if (!this.ServiceBusProcessor.IsClosed)
                        await this.ServiceBusProcessor.CloseAsync();
                }
                finally
                {
                    await this.ServiceBusProcessor.DisposeAsync();
                    this.ServiceBusProcessor = null;
                }
            }
        }

        protected virtual Task StartReceivingInternalAsync(
            string topicPath,
            string subscriptionName,
            ReceivedItemHandlerAsyncDelegate receivedItemHandlerAsyncDelegateFunc,
            bool autoMessageFinalizationEnabled = true,
            CancellationToken cancellationToken = default
        )
        {
            //Ensure we are set to have started Processing...
            this.IsProcessing = true;

            //Validate handler and other references...
            receivedItemHandlerAsyncDelegateFunc.AssertNotNull(nameof(receivedItemHandlerAsyncDelegateFunc));

            //Complete full Handler Initialization with provided options, etc...
            //NOTE: IF FIFO Processing is enabled we use Azure Session Processor; otherwise default Processor is used.
            if (Options.FifoEnforcedReceivingEnabled)
            {
                this.ServiceBusSessionProcessor = GetAzureServiceBusSessionProcessor(
                    topicPath.AssertNotNullOrWhiteSpace(nameof(topicPath)),
                    subscriptionName.AssertNotNullOrWhiteSpace(nameof(subscriptionName))
                );

                this.ServiceBusSessionProcessor.ProcessMessageAsync += async (messageEventArgs) =>
                {
                    await ExecuteMessageHandlerAsync(
                        //Note: We Must use the MessageSession as the Client for processing messages with the Locked Session!
                        messageEventArgs,
                        receivedItemHandlerAsyncDelegateFunc,
                        autoMessageFinalizationEnabled
                    ).ConfigureAwait(false);
                };

                //Wire up the Error/Exception Handler...
                this.ServiceBusSessionProcessor.ProcessErrorAsync += ExceptionReceivedHandlerAsync;

                //Kick off processing!!!
                this.ServiceBusSessionProcessor.StartProcessingAsync(cancellationToken);
            }
            else
            {
                this.ServiceBusProcessor = GetAzureServiceBusProcessor(
                    topicPath.AssertNotNullOrWhiteSpace(nameof(topicPath)),
                    subscriptionName.AssertNotNullOrWhiteSpace(nameof(subscriptionName))
                );

                //Wire up the Handler...
                this.ServiceBusProcessor.ProcessMessageAsync += async (messageEventArgs) =>
                {
                    await ExecuteMessageHandlerAsync(
                        messageEventArgs,
                        receivedItemHandlerAsyncDelegateFunc,
                        autoMessageFinalizationEnabled
                    ).ConfigureAwait(false);
                };

                //Wire up the Error/Exception Handler...
                this.ServiceBusProcessor.ProcessErrorAsync += ExceptionReceivedHandlerAsync;

                //Kick off processing!!!
                this.ServiceBusProcessor.StartProcessingAsync(cancellationToken);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Initialize a dynamic enumerable that will enumerate messages as they are produced from the
        ///  Azure Service Bus. This will waiting for the specified time before releasing for the enumeration to complete;
        ///  when no new items arrive in the time specified.
        /// Note, using this enumeration means that ALL Items will automatically be acknowledged as
        ///  as successfully received with no ability to Reject/Abandon them.
        /// </summary>
        /// <param name="topicPath"></param>
        /// <param name="subscriptionName"></param>
        /// <param name="receiveWaitPerItemTimeout"></param>
        /// <param name="throwExceptionOnCancellation"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
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
                //Set Item to Null to ensure old items don't create an infinite loop...
                item = null;
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
            var dynamicAsyncReceiverQueue = new SqlTransactionalOutboxReceiverQueue<TUniqueIdentifier, TPayload>(
                disposedCallbackAsyncHandler: async () =>
                {
                    await this.StopReceivingAsync().ConfigureAwait(false);
                }
            );

            //Attempt to Receive & Handle the Messages just published for End-to-End Validation!
            this.StartReceivingInternalAsync(
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
                autoMessageFinalizationEnabled: false
            );

            return dynamicAsyncReceiverQueue;
        }

        protected virtual async Task ExecuteMessageHandlerAsync(
            EventArgs messageEventArgs,
            ReceivedItemHandlerAsyncDelegate receivedItemHandlerAsyncDelegateFunc,
            bool automaticFinalizationEnabled = true
        )
        {
            messageEventArgs.AssertNotNull(nameof(messageEventArgs));
            receivedItemHandlerAsyncDelegateFunc.AssertNotNull(nameof(receivedItemHandlerAsyncDelegateFunc));

            AzureServiceBusReceivedItem<TUniqueIdentifier, TPayload> azureServiceBusReceivedItem = null;

            //FIRST attempt to convert the Message into an AzureServiceBus Received Item....
            try
            {
                //Initialize the MessageHandler facade for processing the Azure Service Bus message...
                azureServiceBusReceivedItem = await CreateReceivedItemAsync(
                    messageEventArgs,
                    //Force Dead-lettering if there is any issue with initialization!
                    true
                ).AssertNotNull(nameof(azureServiceBusReceivedItem));
            }
            catch (Exception exc)
            {
                var messageException = new Exception("The message could not be correctly initialized due to unexpected exception." +
                                                     " It must be Dead lettered to prevent blocking of the Service Bus as it will never be" +
                                                     " processed successfully.", exc);

                this.Options.LogErrorCallback?.Invoke(messageException);
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
                    if (!azureServiceBusReceivedItem.IsStatusFinalized)
                    {
                        //Finalize the status as set by the Delegate function if it's not already Finalized!
                        //NOTE: We ALWAYS do this even if autoMessageFinalizationEnabled is false to prevent BLOCKING and risk of Infinite Loops...
                        await azureServiceBusReceivedItem.SendFinalizedStatusToAzureServiceBusAsync().ConfigureAwait(false);
                    }
                }
            }
        }

        protected virtual ServiceBusSessionProcessor GetAzureServiceBusSessionProcessor(string topicPath, string subscriptionName)
        {
            //Configure additional options...
            var sessionProcessorOptions = new ServiceBusSessionProcessorOptions()
            {
                AutoCompleteMessages = false,
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            };

            if (this.Options.PrefetchCount > 0)
                sessionProcessorOptions.PrefetchCount = this.Options.PrefetchCount;

            if (this.Options.MaxConcurrentReceiversOrSessions > 0)
                sessionProcessorOptions.MaxConcurrentSessions = this.Options.MaxConcurrentReceiversOrSessions;

            //Good Detail Summary of this property is at: https://stackoverflow.com/a/60381046/7293142
            if (this.Options.MaxAutoRenewDuration.HasValue)
                sessionProcessorOptions.MaxAutoLockRenewalDuration = this.Options.MaxAutoRenewDuration.Value;

            if (this.Options.SessionConnectionIdleTimeout.HasValue)
                sessionProcessorOptions.SessionIdleTimeout= this.Options.SessionConnectionIdleTimeout.Value;

            var sessionProcessingClient = this.AzureServiceBusClient.CreateSessionProcessor(
                topicName: topicPath.AssertNotNullOrWhiteSpace(nameof(topicPath)),
                subscriptionName: subscriptionName.AssertNotNullOrWhiteSpace(nameof(subscriptionName)),
                sessionProcessorOptions
            );

            //Enable dynamic configuration if specified...
            this.SessionProcessingClientConfigurationFunc?.Invoke(sessionProcessingClient);
            return sessionProcessingClient;
        }


        protected virtual ServiceBusProcessor GetAzureServiceBusProcessor(string topicPath, string subscriptionName)
        {
            //Configure additional options...
            var processorOptions = new ServiceBusProcessorOptions()
            {
                AutoCompleteMessages = false,
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            };

            if (this.Options.PrefetchCount > 0)
                processorOptions.PrefetchCount = this.Options.PrefetchCount;

            if (this.Options.MaxConcurrentReceiversOrSessions > 0)
                processorOptions.MaxConcurrentCalls = this.Options.MaxConcurrentReceiversOrSessions;

            if (this.Options.MaxAutoRenewDuration.HasValue)
                processorOptions.MaxAutoLockRenewalDuration = this.Options.MaxAutoRenewDuration.Value;

            var serviceBusProcessingClient = this.AzureServiceBusClient.CreateProcessor(
                topicName: topicPath.AssertNotNullOrWhiteSpace(nameof(topicPath)),
                subscriptionName: subscriptionName.AssertNotNullOrWhiteSpace(nameof(subscriptionName)),
                processorOptions
            );

            //Enable dynamic configuration if specified...
            this.ProcessingClientConfigurationFunc?.Invoke(serviceBusProcessingClient);
            return serviceBusProcessingClient;
        }

        protected virtual async Task<AzureServiceBusReceivedItem<TUniqueIdentifier, TPayload>> CreateReceivedItemAsync(
            EventArgs baseMessageEventArgs,
            bool deadLetterOnFailureToInitialize = false
        )
        {
            AzureServiceBusReceivedItem<TUniqueIdentifier, TPayload> azureServiceBusReceivedItem = null;
            var initializationErrorMessage = $"Unable to initialize {nameof(SqlTransactionalOutbox)} Received Item [{nameof(AzureServiceBusReceivedItem<TUniqueIdentifier, TPayload>)}]";

            switch (baseMessageEventArgs)
            {
                case ProcessMessageEventArgs messageEventArgs:
                    try
                    {
                        azureServiceBusReceivedItem = new AzureServiceBusReceivedItem<TUniqueIdentifier, TPayload>(
                            messageEventArgs: messageEventArgs,
                            outboxItemFactory: this.OutboxItemFactory
                        );
                    }
                    catch (Exception exc)
                    {
                        if (deadLetterOnFailureToInitialize)
                            await messageEventArgs.DeadLetterMessageAsync(
                                messageEventArgs.Message,
                                initializationErrorMessage,
                                exc.GetMessagesRecursively()
                            );
                        
                        throw;
                    }

                    break;
                case ProcessSessionMessageEventArgs sessionMessageEventArgs:
                    try
                    {
                        azureServiceBusReceivedItem = new AzureServiceBusReceivedItem<TUniqueIdentifier, TPayload>(
                            sessionMessageEventArgs: sessionMessageEventArgs,
                            outboxItemFactory: this.OutboxItemFactory
                        );
                    }
                    catch (Exception exc)
                    {
                        if (deadLetterOnFailureToInitialize)
                            await sessionMessageEventArgs.DeadLetterMessageAsync(
                                sessionMessageEventArgs.Message,
                                initializationErrorMessage,
                                exc.GetMessagesRecursively()
                            );
                        
                        throw;
                    }

                    break;
            }

            return azureServiceBusReceivedItem;
        }

        protected virtual Task ExceptionReceivedHandlerAsync(ProcessErrorEventArgs args)
        {
            var message = $"An unexpected exception occurred while attempting to receive messages from Azure Service Bus;" +
                          $" [EntityPath={args.EntityPath}], [ExecutingAction={args.ErrorSource}]";

            var logException = new Exception(message, args.Exception);

            //Throw the exception if we can't Log it...
            if(this.Options.LogErrorCallback == null)
                Debug.WriteLine(logException.GetMessagesRecursively());
            else
                this.Options.LogErrorCallback(logException);

            return Task.CompletedTask;
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();

            if (DisposingEnabled)
            {
                if (this.ServiceBusProcessor != null)
                    await this.ServiceBusProcessor.DisposeAsync();

                if (this.ServiceBusSessionProcessor!= null)
                    await this.ServiceBusSessionProcessor.DisposeAsync();
            }
        }
    }
}
