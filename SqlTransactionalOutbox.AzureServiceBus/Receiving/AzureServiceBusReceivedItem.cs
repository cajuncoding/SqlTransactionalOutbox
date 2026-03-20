using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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

            //By Definition in handling the ProcessSessionMessageEventArgs, we know that this is a FIFO Session based message as it uses Sessions...
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

            //By Definition in handling the ProcessMessageEventArgs, we know that this is a NOTE a FIFO Session based message as it does not use Sessions...
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
            //Client is OPTIONAL; not necessary when processing will be handled by AzureFunctions framework bindings, etc.
            //  It is only necessary if you desire direct control over the Acknowledgement/Rejection/Dead-lettering of the message
            //  instead of relying on the Azure Functions framework to do it for you when the function returns.
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
                createdDateTimeUtc: azureServiceBusMessage.ApplicationProperties.GetValueSafely<DateTimeOffset>(MessageHeaders.OutboxCreatedDateUtc),
                scheduledPublishDateTimeUtc: azureServiceBusMessage.ApplicationProperties.GetValueSafely<DateTimeOffset?>(MessageHeaders.OutboxScheduledPublishDateUtc),
                status: OutboxItemStatus.Successful.ToString(),
                fifoGroupingIdentifier: azureServiceBusMessage.SessionId,
                publishAttempts: azureServiceBusMessage.ApplicationProperties.GetValueSafely<int>(MessageHeaders.OutboxPublishingAttempts),
                publishTarget: azureServiceBusMessage.ApplicationProperties.GetValueSafely<string>(MessageHeaders.OutboxPublishingTarget),
                serializedPayload: Encoding.UTF8.GetString(azureServiceBusMessage.Body)
            );

            var headersLookup = azureServiceBusMessage.ApplicationProperties?.ToLookup(
                k => k.Key,
                v => v.Value,
                StringComparer.OrdinalIgnoreCase
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

        public virtual async Task SendFinalizedStatusToAzureServiceBusAsync(CancellationToken cancellationToken = default)
        {
            //Finally, we must notify Azure Service Bus to Complete the item or to Abandon as defined by our current status!
            switch (this.Status)
            {
                case OutboxReceivedItemProcessingStatus.AcknowledgeSuccessfulReceipt:
                    await AcknowledgeSuccessfulReceiptAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case OutboxReceivedItemProcessingStatus.RejectAsDeadLetter:
                    await RejectAsDeadLetterAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case OutboxReceivedItemProcessingStatus.RejectAndAbandon:
                default:
                    await RejectAndAbandonAsync(cancellationToken).ConfigureAwait(false);
                    break;
            }
        }

        public override async Task AcknowledgeSuccessfulReceiptAsync(CancellationToken cancellationToken = default)
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

                await this.ServiceBusReceiver.CompleteMessageAsync(this.AzureServiceBusMessage, cancellationToken: cancellationToken);
            }
            else if (this.MessageEventArgs != null)
            {
                await this.MessageEventArgs.CompleteMessageAsync(this.AzureServiceBusMessage, cancellationToken: cancellationToken);
            }
            else if (this.SessionMessageEventArgs != null)
            {
                await this.SessionMessageEventArgs.CompleteMessageAsync(this.AzureServiceBusMessage, cancellationToken: cancellationToken);
            }

            await base.AcknowledgeSuccessfulReceiptAsync(cancellationToken).ConfigureAwait(false);
        }

        public override async Task RejectAndAbandonAsync(CancellationToken cancellationToken = default)
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

                await this.ServiceBusReceiver.AbandonMessageAsync(this.AzureServiceBusMessage, cancellationToken: cancellationToken);
            }
            else if (this.MessageEventArgs != null)
            {
                await this.MessageEventArgs.AbandonMessageAsync(this.AzureServiceBusMessage, cancellationToken: cancellationToken);
            }
            else if (this.SessionMessageEventArgs != null)
            {
                await this.SessionMessageEventArgs.AbandonMessageAsync(this.AzureServiceBusMessage, cancellationToken: cancellationToken);
            }

            await base.RejectAndAbandonAsync(cancellationToken).ConfigureAwait(false);
        }

        public override async Task RejectAsDeadLetterAsync(CancellationToken cancellationToken = default)
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

                await this.ServiceBusReceiver.DeadLetterMessageAsync(this.AzureServiceBusMessage, cancellationToken: cancellationToken);
            }
            else if (this.MessageEventArgs != null)
            {
                await this.MessageEventArgs.DeadLetterMessageAsync(this.AzureServiceBusMessage, cancellationToken: cancellationToken);
            }
            else if (this.SessionMessageEventArgs != null)
            {
                await this.SessionMessageEventArgs.DeadLetterMessageAsync(this.AzureServiceBusMessage, cancellationToken: cancellationToken);
            }

            await base.RejectAsDeadLetterAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
