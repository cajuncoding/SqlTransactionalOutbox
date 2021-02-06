using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.Publishing;
using SqlTransactionalOutbox.Receiving;

namespace SqlTransactionalOutbox.AzureServiceBus.Receiving
{
    public class AzureServiceBusReceivedItem<TUniqueIdentifier, TPayload> : OutboxReceivedItem<TUniqueIdentifier, TPayload>
    {
        private const string AzureServiceBusClientIsClosedErrorMessage =
            "Unable to explicitly reject/abandon receipt of the message because the AzureServiceBusClient" +
            " is closed; the message will be automatically abandoned after the lock timeout is exceeded.";

        public Message AzureServiceBusMessage { get; }

        protected IReceiverClient AzureServiceBusClient { get; }

        public AzureServiceBusReceivedItem(
            Message azureServiceBusMessage,
            ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> outboxItemFactory,
            //Client is OPTIONAL; necessary when processing will be handled by AzureFunctions framework bindings, etc.
            IReceiverClient azureServiceBusClient = null,
            bool isFifoProcessingEnabled = false
        )
        {
            //Provide access to the original Azure Service Bus Message for customized Advanced functionality.
            this.AzureServiceBusMessage = azureServiceBusMessage.AssertNotNull(nameof(azureServiceBusMessage));

            //Client is OPTIONAL; necessary when processing will be handled by AzureFunctions framework bindings, etc.
            this.AzureServiceBusClient = azureServiceBusClient;

            var outboxItem = outboxItemFactory.CreateExistingOutboxItem(
                uniqueIdentifier: azureServiceBusMessage.MessageId,
                createdDateTimeUtc: (DateTime)azureServiceBusMessage.UserProperties[MessageHeaders.OutboxCreatedDateUtc],
                status: OutboxItemStatus.Successful.ToString(),
                publishAttempts: (int)azureServiceBusMessage.UserProperties[MessageHeaders.OutboxPublishingAttempts],
                publishTarget: (string)azureServiceBusMessage.UserProperties[MessageHeaders.OutboxPublishingTarget],
                serializedPayload: Encoding.UTF8.GetString(azureServiceBusMessage.Body)
            );

            var headersLookup = this.AzureServiceBusMessage.UserProperties?.ToLookup(
                k => k.Key,
                v => v.Value
            );

            InitBaseOutboxReceivedItem(
                outboxItem,
                headersLookup,
                azureServiceBusMessage.ContentType,
                isFifoProcessingEnabled,
                fifoGroupingIdentifier: azureServiceBusMessage.SessionId,
                correlationId: azureServiceBusMessage.CorrelationId
            );
        }

        public virtual async Task SendFinalizedStatusToAzureServiceBusAsync()
        {
            //Finally, we must notify Azure Service Bus to Complete the item or to Abandon as defined by the status returned!
            switch (this.Status)
            {
                case OutboxReceivedItemProcessingStatus.AcknowledgeSuccessfulReceipt:
                    await AcknowledgeSuccessfulReceiptAsync().ConfigureAwait(false);
                    break;
                case OutboxReceivedItemProcessingStatus.RejectAsDeadLetter:
                    await RejectAsDeadLetterAsync().ConfigureAwait(false);
                    break;
                case OutboxReceivedItemProcessingStatus.RejectAndAbandon:
                default:
                    await RejectAndAbandonAsync().ConfigureAwait(false);
                    break;
            }
        }

        public override Task AcknowledgeSuccessfulReceiptAsync()
        {
            return ProcessAzureServiceBusStatusAsync(
                (lockToken) => this.AzureServiceBusClient?.CompleteAsync(lockToken)
            );
        }

        public override Task RejectAndAbandonAsync()
        {
            return ProcessAzureServiceBusStatusAsync(
                (lockToken) => this.AzureServiceBusClient?.AbandonAsync(lockToken)
            );
        }

        public override Task RejectAsDeadLetterAsync()
        {
            return ProcessAzureServiceBusStatusAsync(
                (lockToken) => this.AzureServiceBusClient?.DeadLetterAsync(lockToken)
            );
        }

        protected virtual async Task ProcessAzureServiceBusStatusAsync(Func<string, Task> sendStatusFunc)
        {
            //Ensure that we are re-entrant and don't attempt to finalize again...
            //NOTE: With Azure Functions there is no client when used with Function Bindings because this is handled
            //      by the Azure Functions framework when the Message is returned, so this should be a No-op if null/not-specified!
            if (IsStatusFinalized || this.AzureServiceBusClient == null)
                return;

            if (this.AzureServiceBusClient.IsClosedOrClosing)
                throw new Exception(AzureServiceBusClientIsClosedErrorMessage);

            //Acknowledge & Complete the Receipt of the Message!
            var lockToken = this.AzureServiceBusMessage.SystemProperties.LockToken;
            await sendStatusFunc(lockToken).ConfigureAwait(false);

            //Ensure that we are re-entrant and don't attempt to finalize again...
            IsStatusFinalized = true;
        }
    }
}
