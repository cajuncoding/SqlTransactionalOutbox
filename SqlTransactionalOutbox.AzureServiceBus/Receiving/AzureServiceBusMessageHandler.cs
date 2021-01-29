using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.Publishing;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class AzureServiceBusMessageHandler<TUniqueIdentifier, TPayload> 
        : ISqlTransactionalOutboxReceivedItemFactory<TUniqueIdentifier, TPayload>
    {
        private const string AzureServiceBusClientIsClosedErrorMessage = 
            "Unable to explicitly reject/abandon receipt of the message because the AzureServiceBusClient" +
            " is closed; the message will be automatically abandoned after the lock timeout is exceeded.";

        public ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> OutboxItemFactory { get; }

        protected IReceiverClient AzureServiceBusClient { get; }

        protected Message AzureServiceBusMessage { get; }

        protected bool IsMessageStatusFinalized { get; set; } = false;

        /// <summary>
        /// The Handler that enables working with Azure Service Bus Messages and converting to OutboxReceivedItems with
        ///     parsing/processing of SqlTransactionalOutbox conventions, etc.
        /// This handler is compatible with both IClientEntity using AzureServiceBust subscription client as well as
        ///     with AzureFunctions which will automatically handle the Message through the function bindings when returned
        ///     from the Azure Function; in this case the Acknowledge/Reject methods will be safe, but will have no effect.
        /// </summary>
        /// <param name="azureServiceBusMessage"></param>
        /// <param name="outboxItemFactory"></param>
        /// <param name="azureServiceBusClient">Optional for use with AzureFunctions; otherwise required for Acknowledge/Reject methods to have any affect.</param>
        public AzureServiceBusMessageHandler(
            Message azureServiceBusMessage,
            ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> outboxItemFactory,
            IReceiverClient azureServiceBusClient = null
        )
        {
            AzureServiceBusMessage = azureServiceBusMessage.AssertNotNull(nameof(Message));
            OutboxItemFactory = outboxItemFactory.AssertNotNull(nameof(outboxItemFactory));

            //NOTE: With Azure Functions there is no client when used with Function Bindings because this is handled
            //      by the Azure Functions framework when the Message is returned, so this should be a No-op if null/not-specified!
            AzureServiceBusClient = azureServiceBusClient;
        }

        public virtual ISqlTransactionalOutboxReceivedItem<TUniqueIdentifier, TPayload> CreateReceivedOutboxItem()
        {
            var azureServiceBusMessage = this.AzureServiceBusMessage;

            var outboxItem = OutboxItemFactory.CreateExistingOutboxItem(
                uniqueIdentifier: azureServiceBusMessage.MessageId,
                createdDateTimeUtc: (DateTime)azureServiceBusMessage.UserProperties[MessageHeaders.OutboxCreatedDateUtc],
                status: OutboxItemStatus.Successful.ToString(),
                publishAttempts: (int)azureServiceBusMessage.UserProperties[MessageHeaders.OutboxPublishingAttempts],
                publishTarget: (string)azureServiceBusMessage.UserProperties[MessageHeaders.OutboxPublishingTarget],
                serializedPayload: Encoding.UTF8.GetString(azureServiceBusMessage.Body)
            );

            var headersLookup = this.AzureServiceBusMessage.UserProperties.ToLookup(
                k => k.Key,
                v => v.Value
            );

            var outboxReceivedItem = new AzureServiceBusReceivedItem<TUniqueIdentifier, TPayload>(
                azureServiceBusMessage,
                outboxItem,
                headersLookup,
                acknowledgeReceiptAsyncFunc: AcknowledgeSuccessfulReceiptAsync,
                rejectAbandonReceiptAsyncFunc: RejectReceiptAndAbandonAsync,
                rejectDeadLetterReceiptAsyncFunc: RejectReceiptAsDeadLetterAsync,
                parsePayloadFunc: (payloadItem) => OutboxItemFactory.ParsePayload(payloadItem),
                enableFifoEnforcedReceiving: true
            );

            return outboxReceivedItem;
        }

        public virtual async Task SendFinalizedStatusToAzureServiceBusAsync(OutboxReceivedItemProcessingStatus status)
        {
            //Finally, we must notify Azure Service Bus to Complete the item or to Abandon as defined by the status returned!
            switch (status)
            {
                case OutboxReceivedItemProcessingStatus.AcknowledgeSuccessfulReceipt:
                    await AcknowledgeSuccessfulReceiptAsync().ConfigureAwait(false);
                    break;
                case OutboxReceivedItemProcessingStatus.RejectAsDeadLetter:
                    await RejectReceiptAsDeadLetterAsync().ConfigureAwait(false);
                    break;
                case OutboxReceivedItemProcessingStatus.RejectAndAbandon:
                default:
                    await RejectReceiptAndAbandonAsync().ConfigureAwait(false);
                    break;
            }
        }

        protected virtual async Task AcknowledgeSuccessfulReceiptAsync()
        {
            await ProcessAzureServiceBusStatusAsync(
                (lockToken) => this.AzureServiceBusClient.CompleteAsync(lockToken)
            ).ConfigureAwait(false);
        }

        protected virtual async Task RejectReceiptAndAbandonAsync()
        {
            await ProcessAzureServiceBusStatusAsync(
                (lockToken) => this.AzureServiceBusClient.AbandonAsync(lockToken)
            ).ConfigureAwait(false);
        }

        protected virtual async Task RejectReceiptAsDeadLetterAsync()
        {
            await ProcessAzureServiceBusStatusAsync(
                (lockToken) => this.AzureServiceBusClient.DeadLetterAsync(lockToken)
            ).ConfigureAwait(false);
        }

        protected virtual async Task ProcessAzureServiceBusStatusAsync(Func<string, Task> sendStatusFunc)
        {
            //Ensure that we are re-entrant and don't attempt to finalize again...
            //NOTE: With Azure Functions there is no client when used with Function Bindings because this is handled
            //      by the Azure Functions framework when the Message is returned, so this should be a No-op if null/not-specified!
            if (IsMessageStatusFinalized || this.AzureServiceBusClient == null)
                return;

            if (this.AzureServiceBusClient.IsClosedOrClosing)
                throw new Exception(AzureServiceBusClientIsClosedErrorMessage);

            //Acknowledge & Complete the Receipt of the Message!
            var lockToken = this.AzureServiceBusMessage.SystemProperties.LockToken;
            await sendStatusFunc(lockToken).ConfigureAwait(false);

            //Ensure that we are re-entrant and don't attempt to finalize again...
            IsMessageStatusFinalized = true;
        }
    }
}
