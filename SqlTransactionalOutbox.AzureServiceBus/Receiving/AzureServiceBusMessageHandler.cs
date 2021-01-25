using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.Interfaces;
using SqlTransactionalOutbox.Publishing;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class AzureServiceBusMessageHandler<TUniqueIdentifier, TPayload>
    {
        protected ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> OutboxItemFactory { get; }

        protected IReceiverClient AzureReceiverClient { get; }

        protected Message AzureServiceBusMessage { get; }

        public AzureServiceBusMessageHandler(
            Message azureServiceBusMessage,
            IReceiverClient azureServiceBusReceiverClient,
            ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> outboxItemFactory
        )
        {
            AzureServiceBusMessage = azureServiceBusMessage.AssertNotNull(nameof(Message));
            AzureReceiverClient = azureServiceBusReceiverClient.AssertNotNull(nameof(azureServiceBusReceiverClient));
            OutboxItemFactory = outboxItemFactory.AssertNotNull(nameof(outboxItemFactory));
        }

        public virtual ISqlTransactionalOutboxReceivedMessage<TUniqueIdentifier, TPayload> CreateOutboxReceivedItem()
        {
            var outboxItem = CreateOutboxItemFromAzureServiceBusMessage(this.AzureServiceBusMessage);

            var outboxReceivedItem = new OutboxReceivedItem<TUniqueIdentifier, TPayload>(outboxItem, this.OutboxItemFactory);

            return outboxReceivedItem;
        }

        protected virtual ISqlTransactionalOutboxItem<TUniqueIdentifier> CreateOutboxItemFromAzureServiceBusMessage(Message azureServiceBusMessage)
        {
            var outboxItem = OutboxItemFactory.CreateExistingOutboxItem(
                uniqueIdentifier: azureServiceBusMessage.MessageId,
                createdDateTimeUtc: (DateTime)azureServiceBusMessage.UserProperties[MessageHeaders.OutboxCreatedDateUtc],
                status: OutboxItemStatus.Successful.ToString(),
                publishingAttempts: (int)azureServiceBusMessage.UserProperties[MessageHeaders.OutboxPublishingAttempts],
                publishingTarget: (string)azureServiceBusMessage.UserProperties[MessageHeaders.OutboxPublishingTarget],
                serializedPayload: Encoding.UTF8.GetString(azureServiceBusMessage.Body)
            );

            return outboxItem;
        }
        public virtual async Task AcknowledgeSuccessfulReceiptAsync()
        {
            if(this.AzureReceiverClient.IsClosedOrClosing)
                throw new Exception("Unable to Acknowledge receipt of the message because the AzureReceiverClient is closed;" +
                                    "the message will be automatically abandoned after the lock timeout is exceeded.");

            //Acknowledge & Complete the Receipt of the Message!
            var lockToken = this.AzureServiceBusMessage.SystemProperties.LockToken;
            await this.AzureReceiverClient.CompleteAsync(lockToken).ConfigureAwait(false);
        }

        public virtual async Task RejectReceiptAndAbandonAsync()
        {
            if (this.AzureReceiverClient.IsClosedOrClosing)
                throw new Exception("Unable to explicitly reject/abandon receipt of the message because the AzureReceiverClient" +
                                    " is closed; the message will be automatically abandoned after the lock timeout is exceeded.");

            //Acknowledge & Complete the Receipt of the Message!
            var lockToken = this.AzureServiceBusMessage.SystemProperties.LockToken;
            await this.AzureReceiverClient.AbandonAsync(lockToken).ConfigureAwait(false);
        }
    }
}
