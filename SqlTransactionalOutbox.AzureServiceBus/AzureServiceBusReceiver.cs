using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        protected ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> OutboxItemFactory { get; }
        protected AzureReceiverClientCache ReceiverClientCache { get; }

        public AzureServiceBusReceiver(
            string azureServiceBusConnectionString,
            ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> outboxItemFactory
        )
        {
            this.AzureServiceBusConnection = new ServiceBusConnection(
                azureServiceBusConnectionString.AssertNotNullOrWhiteSpace(nameof(azureServiceBusConnectionString))
            );
            this.OutboxItemFactory = outboxItemFactory.AssertNotNull(nameof(outboxItemFactory));
            this.ReceiverClientCache = new AzureReceiverClientCache();
        }

        public virtual IReceiverClient RegisterReceiverHandler(
            string topicPath,
            string subscriptionName,
            Func<ISqlTransactionalOutboxReceivedMessage<TUniqueIdentifier, TPayload>, Task> receivedItemHandlerAsyncFunc
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

                    //Initialize the Received Item wrapper for Azure Service Bus!
                    //NOTE: Always ensure we properly dispose of the ISqlTransactionalOutboxReceivedItem<> after processing!
                    await using var azureServiceBusReceivedItem = CreateOutboxReceivedItem(message, receiverClient);

                    //TODO: Maybe simplify this (reduce code, etc.) to just support returning Enumeration status
                    //  and/or allow thrown exceptions as the client deems fit???
                    //Delegate the Azure Service Bus 'Received Item' implementation to the handler for processing...
                    await receivedItemHandlerAsyncFunc(azureServiceBusReceivedItem).ConfigureAwait(false);

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
                newReceiverClientFactory:() => CreateReceiverClient(topicPath, subscriptionName)
            );

            return receiverClient;
        }

        protected virtual IReceiverClient CreateReceiverClient(string topicPath, string subscriptionName)
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

        protected virtual ISqlTransactionalOutboxReceivedMessage<TUniqueIdentifier, TPayload> CreateOutboxReceivedItem(
            Message message,
            IReceiverClient receiverClient
        )
        {
            var outboxItem = CreateOutboxItemFromAzureServiceBusMessage(message);

            var outboxReceivedItem = new OutboxReceivedItem<TUniqueIdentifier, TPayload>(
                outboxItem,
                acknowledgeReceiptAsyncFunc: async () => await AcknowledgeReceiptHandlerAsync(message, receiverClient).ConfigureAwait(false),
                rejectReceiptAsyncFunc: async () => await RejectReceiptHandlerAsync(message, receiverClient).ConfigureAwait(false),
                parsePayloadFunc: ParsePayload
            );

            return outboxReceivedItem;
        }

        protected virtual ISqlTransactionalOutboxItem<TUniqueIdentifier> CreateOutboxItemFromAzureServiceBusMessage(Message azureServiceBusMessage)
        {
            var outboxItem = OutboxItemFactory.CreateExistingOutboxItem(
                uniqueIdentifier: azureServiceBusMessage.MessageId,
                createdDateTimeUtc: (DateTime) azureServiceBusMessage.UserProperties[MessageHeaders.OutboxCreatedDateUtc],
                status: OutboxItemStatus.Successful.ToString(),
                publishingAttempts: (int) azureServiceBusMessage.UserProperties[MessageHeaders.OutboxPublishingAttempts],
                publishingTarget: (string) azureServiceBusMessage.UserProperties[MessageHeaders.OutboxPublishingTarget],
                serializedPayload: Encoding.UTF8.GetString(azureServiceBusMessage.Body)
            );

            return outboxItem;
        }

        protected virtual async Task AcknowledgeReceiptHandlerAsync(Message message, IReceiverClient receiverClient)
        {
            //Acknowledge & Complete the Receipt of the Message!
            await receiverClient.CompleteAsync(message.SystemProperties.LockToken).ConfigureAwait(false);
        }

        protected virtual async Task RejectReceiptHandlerAsync(Message message, IReceiverClient receiverClient)
        {
            //Acknowledge & Complete the Receipt of the Message!
            await receiverClient.AbandonAsync(message.SystemProperties.LockToken).ConfigureAwait(false);
        }

        protected virtual TPayload ParsePayload(ISqlTransactionalOutboxItem<TUniqueIdentifier> outboxItem)
        {
            //NOTE: The Factory encapsulates processing/parsing logic, which may include support for compression, etc.
            var payload = OutboxItemFactory.ParsePayload(outboxItem);
            return payload;
        }

        protected virtual Task ExceptionReceivedHandlerAsync(ExceptionReceivedEventArgs args)
        {
            throw new Exception("An unexpected exception occurred while attempting to receive messages from Azure Service Bus.", args.Exception);
        }
    }
}
