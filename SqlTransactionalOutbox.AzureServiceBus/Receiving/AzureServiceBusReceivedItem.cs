using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.Publishing;
using SqlTransactionalOutbox.Receiving;

namespace SqlTransactionalOutbox.AzureServiceBus.Receiving
{
    public class AzureServiceBusReceivedItem<TUniqueIdentifier, TPayloadBody> : OutboxReceivedItem<TUniqueIdentifier, TPayloadBody>
    {
        private const string AzureServiceBusClientIsClosedErrorMessage =
            "Unable to explicitly reject/abandon receipt of the message because the AzureServiceBusClient" +
            " is closed; the message will be automatically abandoned after the lock timeout is exceeded.";

        public ServiceBusReceivedMessage AzureServiceBusMessage { get; }

        protected ProcessSessionMessageEventArgs SessionMessageEventArgs { get; }
        
        protected ProcessMessageEventArgs MessageEventArgs { get; }

        protected ServiceBusReceiver ServiceBusReceiver { get; }

        protected ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayloadBody> OutboxItemFactory { get; }

        /// <summary>
        /// Initialize a received item with all Sql Transactional Outbox data.  The resulting item will
        ///     allow for specific handling of the Message Acknowledgement/Rejection/Dead-lettering just as.
        ///     the ProcessMessageEventArgs allow...
        /// </summary>
        /// <param name="sessionMessageEventArgs"></param>
        /// <param name="outboxItemFactory"></param>
        public AzureServiceBusReceivedItem(
            ProcessSessionMessageEventArgs sessionMessageEventArgs,
            ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayloadBody> outboxItemFactory
        )
        {
            this.SessionMessageEventArgs = sessionMessageEventArgs.AssertNotNull(nameof(sessionMessageEventArgs));
            this.AzureServiceBusMessage = sessionMessageEventArgs.Message;
            this.OutboxItemFactory = outboxItemFactory.AssertNotNull(nameof(outboxItemFactory));
            
            this.InitAzureServiceBusOutboxReceivedItem(isFifoProcessingEnabled: true);
        }

        /// <summary>
        /// Initialize a received item with all Sql Transactional Outbox data.  The resulting item will
        ///     allow for specific handling of the Message Acknowledgement/Rejection/Dead-lettering just as.
        ///     the ProcessMessageEventArgs allow...
        /// </summary>
        /// <param name="messageEventArgs"></param>
        /// <param name="outboxItemFactory"></param>
        public AzureServiceBusReceivedItem(
            ProcessMessageEventArgs messageEventArgs,
            ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayloadBody> outboxItemFactory
        )
        {
            this.MessageEventArgs = messageEventArgs.AssertNotNull(nameof(messageEventArgs));
            this.AzureServiceBusMessage = messageEventArgs.Message;
            this.OutboxItemFactory = outboxItemFactory.AssertNotNull(nameof(outboxItemFactory));

            this.InitAzureServiceBusOutboxReceivedItem(isFifoProcessingEnabled: false);
        }


        /// <summary>
        /// Initialize a received item with all Sql Transactional Outbox data.  The resulting item will
        ///     allow for specific handling of the Message Acknowledgement/Rejection/Dead-lettering if the correct Reciever is provided.
        /// NOTE: With Azure Functions there is no client when used with Function Bindings because this is handled
        ///     by the Azure Functions framework when the Message is returned, so this should be a No-op if null/not-specified!
        /// </summary>
        /// <param name="azureServiceBusMessage"></param>
        /// <param name="outboxItemFactory"></param>
        /// <param name="azureServiceBusReceiverClient"></param>
        public AzureServiceBusReceivedItem(
            ServiceBusReceivedMessage azureServiceBusMessage,
            ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayloadBody> outboxItemFactory,
            //Client is OPTIONAL; necessary when processing will be handled by AzureFunctions framework bindings, etc.
            ServiceBusReceiver azureServiceBusReceiverClient = null
        )
        {
            //Provide access to the original Azure Service Bus Message for customized Advanced functionality.
            this.AzureServiceBusMessage = azureServiceBusMessage.AssertNotNull(nameof(azureServiceBusMessage));
            this.ServiceBusReceiver = azureServiceBusReceiverClient;
            this.OutboxItemFactory = outboxItemFactory.AssertNotNull(nameof(outboxItemFactory));

            bool isFifoProcessingEnabled = azureServiceBusReceiverClient is ServiceBusSessionReceiver;
            this.InitAzureServiceBusOutboxReceivedItem(isFifoProcessingEnabled);
        }

        protected void InitAzureServiceBusOutboxReceivedItem(bool isFifoProcessingEnabled)
        {
            var azureServiceBusMessage = this.AzureServiceBusMessage;

            var outboxItem = this.OutboxItemFactory.CreateExistingOutboxItem(
                uniqueIdentifier: azureServiceBusMessage.MessageId,
                createdDateTimeUtc: (DateTimeOffset)azureServiceBusMessage.ApplicationProperties[MessageHeaders.OutboxCreatedDateUtc],
                status: OutboxItemStatus.Successful.ToString(),
                fifoGroupingIdentifier: azureServiceBusMessage.SessionId,
                publishAttempts: (int)azureServiceBusMessage.ApplicationProperties[MessageHeaders.OutboxPublishingAttempts],
                publishTarget: (string)azureServiceBusMessage.ApplicationProperties[MessageHeaders.OutboxPublishingTarget],
                serializedPayload: Encoding.UTF8.GetString(azureServiceBusMessage.Body)
            );

            var headersLookup = azureServiceBusMessage.ApplicationProperties?.ToLookup(
                k => k.Key,
                v => v.Value
            );

            InitBaseOutboxReceivedItem(
                outboxItem,
                headersLookup,
                azureServiceBusMessage.ContentType,
                this.OutboxItemFactory.ParsePayload,
                isFifoProcessingEnabled: isFifoProcessingEnabled,
                subject: azureServiceBusMessage.Subject,
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

        public override async Task AcknowledgeSuccessfulReceiptAsync()
        {
            //Ensure that we are re-entrant and don't attempt to finalize again...
            //NOTE: With Azure Functions there is no client when used with Function Bindings because this is handled
            //      by the Azure Functions framework when the Message is returned, so this should be a No-op if null/not-specified!
            if (IsStatusFinalized)
                return;

            if (this.ServiceBusReceiver != null)
            {
                if (this.ServiceBusReceiver.IsClosed)
                    throw new Exception(AzureServiceBusClientIsClosedErrorMessage);

                await this.ServiceBusReceiver.CompleteMessageAsync(this.AzureServiceBusMessage);
            }
            else if (this.MessageEventArgs != null)
            {
                await this.MessageEventArgs.CompleteMessageAsync(this.AzureServiceBusMessage);
            }
            else if (this.SessionMessageEventArgs != null)
            {
                await this.SessionMessageEventArgs.CompleteMessageAsync(this.AzureServiceBusMessage);
            }

            await base.AcknowledgeSuccessfulReceiptAsync().ConfigureAwait(false);
        }

        public override async Task RejectAndAbandonAsync()
        {
            //Ensure that we are re-entrant and don't attempt to finalize again...
            //NOTE: With Azure Functions there is no client when used with Function Bindings because this is handled
            //      by the Azure Functions framework when the Message is returned, so this should be a No-op if null/not-specified!
            if (IsStatusFinalized)
                return;

            if (this.ServiceBusReceiver != null)
            {
                if (this.ServiceBusReceiver.IsClosed)
                    throw new Exception(AzureServiceBusClientIsClosedErrorMessage);

                await this.ServiceBusReceiver.AbandonMessageAsync(this.AzureServiceBusMessage);
            }
            else if (this.MessageEventArgs != null)
            {
                await this.MessageEventArgs.AbandonMessageAsync(this.AzureServiceBusMessage);
            }
            else if (this.SessionMessageEventArgs != null)
            {
                await this.SessionMessageEventArgs.AbandonMessageAsync(this.AzureServiceBusMessage);
            }

            await base.RejectAndAbandonAsync().ConfigureAwait(false);
        }

        public override async Task RejectAsDeadLetterAsync()
        {
            //Ensure that we are re-entrant and don't attempt to finalize again...
            //NOTE: With Azure Functions there is no client when used with Function Bindings because this is handled
            //      by the Azure Functions framework when the Message is returned, so this should be a No-op if null/not-specified!
            if (IsStatusFinalized)
                return;

            if (this.ServiceBusReceiver != null)
            {
                if (this.ServiceBusReceiver.IsClosed)
                    throw new Exception(AzureServiceBusClientIsClosedErrorMessage);

                await this.ServiceBusReceiver.DeadLetterMessageAsync(this.AzureServiceBusMessage);
            }
            else if (this.MessageEventArgs != null)
            {
                await this.MessageEventArgs.DeadLetterMessageAsync(this.AzureServiceBusMessage);
            }
            else if (this.SessionMessageEventArgs != null)
            {
                await this.SessionMessageEventArgs.DeadLetterMessageAsync(this.AzureServiceBusMessage);
            }

            await base.RejectAsDeadLetterAsync().ConfigureAwait(false);
        }
    }
}
