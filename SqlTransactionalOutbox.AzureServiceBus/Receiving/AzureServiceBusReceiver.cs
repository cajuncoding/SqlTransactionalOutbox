using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Amqp.Serialization;
using SqlTransactionalOutbox.AzureServiceBus.Common;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.Receiving;

namespace SqlTransactionalOutbox.AzureServiceBus.Receiving
{
    public class AzureServiceBusReceiver<TUniqueIdentifier, TPayload> : BaseAzureServiceBusClient, IAsyncDisposable
    {
        public delegate Task ReceivedItemHandlerAsyncDelegate(ISqlTransactionalOutboxReceivedItem<TUniqueIdentifier, TPayload> outboxReceivedItem);

        public string ServiceBusSubscription { get; set; }

        public AzureServiceBusReceivingOptions Options { get; }

        protected ServiceBusSessionProcessor ServiceBusSessionProcessor { get; set; } = null;

        protected ServiceBusProcessor ServiceBusProcessor { get; set; } = null;
        
        protected bool IsProcessing { get; set; } = false;

        protected ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> OutboxItemFactory { get; }

        public AzureServiceBusReceiver(
            string azureServiceBusConnectionString,
            string serviceBusTopic,
            string serviceBusSubscription,
            ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> outboxItemFactory,
            AzureServiceBusReceivingOptions options = null
        ) : this(InitAzureServiceBusConnection(azureServiceBusConnectionString, options), serviceBusTopic, serviceBusSubscription, outboxItemFactory, options)
        {
            DisposingEnabled = true;
        }

        public AzureServiceBusReceiver(
            ServiceBusClient azureServiceBusClient,
            string serviceBusTopic,
            string serviceBusSubscription,
            ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> outboxItemFactory,
            AzureServiceBusReceivingOptions options = null
        )
        {
            this.Options = options ?? new AzureServiceBusReceivingOptions();
            this.AzureServiceBusClient = azureServiceBusClient.AssertNotNull(nameof(azureServiceBusClient));
            this.OutboxItemFactory = outboxItemFactory.AssertNotNull(nameof(outboxItemFactory));

            this.ServiceBusTopic = serviceBusTopic.AssertNotNullOrWhiteSpace(nameof(serviceBusTopic));
            this.ServiceBusSubscription = serviceBusSubscription.AssertNotNullOrWhiteSpace(nameof(serviceBusSubscription));
        }

        public virtual Task StartReceivingAsync(
            ISqlTransactionalOutboxReceivedItemHandler<TUniqueIdentifier, TPayload> receivedItemHandler,
            CancellationToken cancellationToken = default
        )
        {
            if (this.IsProcessing) 
                return Task.CompletedTask;
            
            receivedItemHandler.AssertNotNull(nameof(receivedItemHandler));

            return StartReceivingInternalAsync(receivedItemHandler.HandleReceivedItemAsync, cancellationToken: cancellationToken);
        }

        public virtual Task StartReceivingAsync(
            ReceivedItemHandlerAsyncDelegate receivedItemHandlerAsyncDelegateFunc,
            CancellationToken cancellationToken = default
        )
        {
            if (this.IsProcessing) 
                return Task.CompletedTask;

            return StartReceivingInternalAsync(receivedItemHandlerAsyncDelegateFunc, cancellationToken: cancellationToken);
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

        protected virtual async Task StartReceivingInternalAsync(
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
                if (this.ServiceBusSessionProcessor == null)
                {
                    this.ServiceBusSessionProcessor = GetAzureServiceBusSessionProcessor();

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
                }

                //Kick off processing!!!
                if(!this.ServiceBusSessionProcessor.IsProcessing)
                    await this.ServiceBusSessionProcessor.StartProcessingAsync(cancellationToken);
            }
            else
            {
                if (this.ServiceBusProcessor == null)
                {
                    this.ServiceBusProcessor = GetAzureServiceBusProcessor();

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
                }

                //Kick off processing!!!
                if (!this.ServiceBusProcessor.IsProcessing)
                    await this.ServiceBusProcessor.StartProcessingAsync(cancellationToken);
            }
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
        public virtual async IAsyncEnumerable<ISqlTransactionalOutboxReceivedItem<TUniqueIdentifier, TPayload>> AsAsyncEnumerable(
            TimeSpan receiveWaitPerItemTimeout,
            bool throwExceptionOnCancellation = false
        )
        {
            if (receiveWaitPerItemTimeout == default)
            {
                throw new ArgumentException("Wait time must be specified.", nameof(receiveWaitPerItemTimeout));
            }

            //ENSURE WE DISPOSE of the Queue when we are done!
            await using var dynamicReceiverQueue = await CreateReceiverQueueAsync();

            //Enumerate data, and Wait for the specified time for it to flow...
            await foreach (var item in dynamicReceiverQueue.AsAsyncEnumerable(receiveWaitPerItemTimeout))
            {
                yield return item;
            }
        }

        /// <summary>
        /// Initialize a producer/consumer queue for asynchronously & dynamically receiving items produced from the
        ///  Azure Service Bus.  Note, using this queue means that ALL Items will automatically be acknowledged as
        ///  as successfully received with no ability to Reject/Abandon them.
        /// </summary>
        protected virtual async Task<SqlTransactionalOutboxReceiverQueue<TUniqueIdentifier, TPayload>> CreateReceiverQueueAsync()
        {
            //Initialize the producer/consumer queue for asynchronously & dynamically receiving items produced from the
            //  Azure Service Bus by being populated from our handler.
            var dynamicAsyncReceiverQueue = new SqlTransactionalOutboxReceiverQueue<TUniqueIdentifier, TPayload>(
                disposedCallbackAsyncHandler: async () =>
                {
                    await this.StopReceivingAsync().ConfigureAwait(false);
                }
            );

            //Attempt to Receive & Handle the Messages just published for End-to-End Validation!
            await this.StartReceivingInternalAsync(
                receivedItemHandlerAsyncDelegateFunc: async (outboxPublishedItem) =>
                {
                    //All our handler needs to do is marshall the resulting item into the Queue to
                    //  signal/push for the consumer to handle...
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

                this.Options.ErrorHandlerCallback?.Invoke(messageException);
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
                    this.Options.ErrorHandlerCallback?.Invoke(exc);

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

        protected virtual ServiceBusSessionProcessor GetAzureServiceBusSessionProcessor()
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
                this.ServiceBusTopic,
                this.ServiceBusSubscription,
                sessionProcessorOptions
            );

            return sessionProcessingClient;
        }

        protected virtual ServiceBusProcessor GetAzureServiceBusProcessor()
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
                this.ServiceBusTopic,
                this.ServiceBusSubscription,
                processorOptions
            );

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
                        azureServiceBusReceivedItem = new AzureServiceBusReceivedItem<TUniqueIdentifier, TPayload>(messageEventArgs, this.OutboxItemFactory);
                    }
                    catch (Exception exc)
                    {
                        if (deadLetterOnFailureToInitialize)
                            await messageEventArgs.DeadLetterMessageAsync(messageEventArgs.Message, initializationErrorMessage, exc.GetMessagesRecursively());
                        
                        throw;
                    }
                    break;
                
                case ProcessSessionMessageEventArgs sessionMessageEventArgs:
                    try
                    {
                        azureServiceBusReceivedItem = new AzureServiceBusReceivedItem<TUniqueIdentifier, TPayload>(sessionMessageEventArgs, this.OutboxItemFactory);
                    }
                    catch (Exception exc)
                    {
                        if (deadLetterOnFailureToInitialize)
                            await sessionMessageEventArgs.DeadLetterMessageAsync(sessionMessageEventArgs.Message, initializationErrorMessage, exc.GetMessagesRecursively());
                        
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
            if(this.Options.ErrorHandlerCallback == null)
                Debug.WriteLine(logException.GetMessagesRecursively());
            else
                this.Options.ErrorHandlerCallback(logException);

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
