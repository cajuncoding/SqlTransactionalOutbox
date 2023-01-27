using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Newtonsoft.Json.Linq;
using SqlTransactionalOutbox.AzureServiceBus.Common;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.JsonExtensions;
using SqlTransactionalOutbox.Publishing;


namespace SqlTransactionalOutbox.AzureServiceBus.Publishing
{
    public class BaseAzureServiceBusPublisher<TUniqueIdentifier> : BaseAzureServiceBusClient, ISqlTransactionalOutboxPublisher<TUniqueIdentifier>, IAsyncDisposable
    {
        public AzureServiceBusPublishingOptions Options { get; }
        public string SenderApplicationName { get; protected set; }

        public BaseAzureServiceBusPublisher(
            string azureServiceBusConnectionString,
            AzureServiceBusPublishingOptions options = null
        ) : this(InitAzureServiceBusConnection(azureServiceBusConnectionString, options), options)
        {
            DisposingEnabled = true;
        }

        public BaseAzureServiceBusPublisher(
            ServiceBusClient azureServiceBusClient,
            AzureServiceBusPublishingOptions options = null
        )
        {
            this.Options = options ?? new AzureServiceBusPublishingOptions();
            this.AzureServiceBusClient = azureServiceBusClient.AssertNotNull(nameof(azureServiceBusClient));
            this.SenderApplicationName = Options.SenderApplicationName;
        }

        public async Task PublishOutboxItemAsync(
            ISqlTransactionalOutboxItem<TUniqueIdentifier> outboxItem, 
            bool isFifoEnforcedProcessingEnabled = false
        )
        {
            var message = CreateEventBusMessage(outboxItem);

            if (isFifoEnforcedProcessingEnabled && string.IsNullOrWhiteSpace(message.SessionId))
            {
                var sessionGuid = Guid.NewGuid();
                Options.LogDebugCallback?.Invoke($"WARNING: FIFO Processing is Enabled but the Outbox Item [{outboxItem.UniqueIdentifier}]" +
                                                 $" does not have a valid FifoGrouping Identifier (e.g. SessionId for Azure Service Bus); " +
                                                 $" so delivery may fail therefore a surrogate Session GUID [{sessionGuid}] as been assigned to ensure delivery.");
                
                message.SessionId = sessionGuid.ToString();
            }

            Options.LogDebugCallback?.Invoke($"Initializing Sender Client for Topic [{outboxItem.PublishTarget}]...");
            await using var senderClient = this.AzureServiceBusClient.CreateSender(outboxItem.PublishTarget);

            var uniqueIdString = ConvertUniqueIdentifierToString(outboxItem.UniqueIdentifier);
            Options.LogDebugCallback?.Invoke($"Sending the Message [{message.Subject}] for outbox item [{uniqueIdString}]...");

            await senderClient.SendMessageAsync(message);

            Options.LogDebugCallback?.Invoke($"Azure Service Bus message [{message.Subject}] has been published successfully.");
        }

        protected virtual ServiceBusMessage CreateEventBusMessage(ISqlTransactionalOutboxItem<TUniqueIdentifier> outboxItem)
        {
            var uniqueIdString = ConvertUniqueIdentifierToString(outboxItem.UniqueIdentifier);
            var defaultLabel = $"[PublishedBy={Options.SenderApplicationName}][MessageId={uniqueIdString}]";
            ServiceBusMessage message = null;

            Options.LogDebugCallback?.Invoke($"Creating Azure Message Object from [{uniqueIdString}]...");

            //Optional FifoGrouping ID From Outbox Table should be used if specified...
            var fifoGroupingId = !string.IsNullOrWhiteSpace(outboxItem.FifoGroupingIdentifier)
                ? outboxItem.FifoGroupingIdentifier.Trim()
                : null;

            //Attempt to decode teh Message as JObject to see if it contains dynamically defined parameters
            //that need to be mapped into the message; otherwise if it's just a string we populate only the minimum.
            var json = ParsePayloadAsJsonSafely(outboxItem);
            if (json != null)
            {
                Options.LogDebugCallback?.Invoke($"Payload for [{uniqueIdString}] is valid Json; initializing dynamic Message Parameters from the Json root properties...");

                //Get the session id; because it is needed to determine PartitionKey when defined...
                fifoGroupingId ??= GetJsonValueSafely(json, JsonMessageFields.FifoGroupingId, (string)null)
                                             ?? GetJsonValueSafely(json, JsonMessageFields.SessionId, (string)null);

                message = new ServiceBusMessage
                {
                    MessageId = uniqueIdString,
                    SessionId = fifoGroupingId,
                    CorrelationId = GetJsonValueSafely(json, JsonMessageFields.CorrelationId, string.Empty),
                    To = GetJsonValueSafely(json, JsonMessageFields.To, string.Empty),
                    ReplyTo = GetJsonValueSafely(json, JsonMessageFields.ReplyTo, string.Empty),
                    ReplyToSessionId = GetJsonValueSafely(json, JsonMessageFields.ReplyToSessionId, string.Empty),
                    //NOTE: IF SessionId is Defined then the Partition Key MUST MATCH the SessionId...
                    PartitionKey = fifoGroupingId ?? GetJsonValueSafely(json, JsonMessageFields.PartitionKey, string.Empty),
                    //Transaction related Partition Key... unused so disabling this to minimize risk.
                    //ViaPartitionKey = sessionId ?? GetJsonValueSafely(json, "viaPartitionKey", string.Empty),
                    ContentType = GetJsonValueSafely(json, JsonMessageFields.ContentType, MessageContentTypes.Json),
                    Subject = GetJsonValueSafely<string>(json, JsonMessageFields.Subject)
                                ?? GetJsonValueSafely(json, JsonMessageFields.Label, defaultLabel)
                };

                //Initialize the Body from dynamic Json if defined, or fallback to the entire body...
                var messageBody = GetJsonValueSafely(json, JsonMessageFields.Body, outboxItem.Payload);
                message.Body = new BinaryData(ConvertPublishingPayloadToBytes(messageBody));

                //Populate HeadersLookup/User Properties if defined dynamically...
                var headers = GetJsonValueSafely<JObject>(json, JsonMessageFields.Headers)
                                        ?? GetJsonValueSafely<JObject>(json, JsonMessageFields.AppProperties)
                                        ?? GetJsonValueSafely<JObject>(json, JsonMessageFields.UserProperties);

                if (headers != null)
                {
                    foreach (JProperty prop in headers.Properties())
                        message.ApplicationProperties.Add(MessageHeaders.ToHeader(prop.Name.ToLower()), prop.Value.ToString());
                }

            }
            else
            //Process as a string with no additional dynamic fields...
            {
                Options.LogDebugCallback?.Invoke($"Payload for [{uniqueIdString}] is in plain text format.");

                message = new ServiceBusMessage
                {
                    MessageId = uniqueIdString,
                    SessionId = fifoGroupingId,
                    CorrelationId = string.Empty,
                    Subject = defaultLabel,
                    ContentType = MessageContentTypes.PlainText,
                    Body = new BinaryData(ConvertPublishingPayloadToBytes(outboxItem.Payload))
                };
            }

            //Add all default headers/user-properties...
            var messageProps = message.ApplicationProperties;
            messageProps.TryAdd(MessageHeaders.ProcessorType, nameof(SqlTransactionalOutbox));
            messageProps.TryAdd(MessageHeaders.ProcessorSender, this.SenderApplicationName);
            messageProps.TryAdd(MessageHeaders.OutboxUniqueIdentifier, uniqueIdString);
            messageProps.TryAdd(MessageHeaders.OutboxCreatedDateUtc, outboxItem.CreatedDateTimeUtc);
            messageProps.TryAdd(MessageHeaders.OutboxPublishingAttempts, outboxItem.PublishAttempts);
            messageProps.TryAdd(MessageHeaders.OutboxPublishingTarget, outboxItem.PublishTarget);
            
            return message;
        }

        protected virtual string ConvertUniqueIdentifierToString(TUniqueIdentifier uniqueIdentifier)
        {
            return uniqueIdentifier.ToString();
        }

        protected virtual byte[] ConvertPublishingPayloadToBytes(string publishingPayload)
        {
            var bytes = Encoding.UTF8.GetBytes(publishingPayload);
            return bytes;
        }

        protected virtual TValue GetJsonValueSafely<TValue>(JObject json, string fieldName, TValue defaultValue = default)
        {
            var value = json.ValueSafely(fieldName, defaultValue);
            return value;
        }

        protected virtual JObject ParsePayloadAsJsonSafely(ISqlTransactionalOutboxItem<TUniqueIdentifier> outboxItem)
        {
            try
            {
                var json = JObject.Parse(outboxItem.Payload);
                return json;
            }
            catch (Exception exc)
            {
                if (Options.ThrowExceptionOnJsonPayloadParseFailure)
                {
                    throw new ArgumentException(
                        $"Json parsing failure; the publishing payload for item [{outboxItem.UniqueIdentifier}] could not" +
                        $" be be parsed as Json.", 
                        exc
                    );
                }

                return null;
            }
        }
    }
}
