using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using SqlTransactionalOutbox.AzureServiceBus.Common;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.JsonExtensions;
using SqlTransactionalOutbox.Publishing;
using SystemTextJsonHelpers;

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
            bool isFifoEnforcedProcessingEnabled = false,
            CancellationToken cancellationToken = default
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

            await senderClient.SendMessageAsync(message, cancellationToken);

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

            //Attempt to decode teh Message as Json to see if it contains dynamically defined parameters
            //that need to be mapped into the message; otherwise if it's just a string we populate only the minimum.
            var json = ParsePayloadAsJsonSafely(outboxItem);
            if (json != null) //Process the payload as Json with potential dynamic parameters for the message...
            {
                Options.LogDebugCallback?.Invoke($"Payload for [{uniqueIdString}] is valid Json; initializing dynamic Message Parameters from the Json root properties...");

                //Get the session id; because it is needed to determine PartitionKey when defined...
                fifoGroupingId ??= GetJsonValueSafely(json, JsonMessageFields.FifoGroupingId, (string)null)
                    ?? GetJsonValueSafely(json, JsonMessageFields.SessionId, (string)null);

                //Get the Scheduled Publish Time with dynamic fallback -- this is likely redundant as this is already handled when building hte Outbox Item,
                //  but this is just in case it was not properly set at the Outbox Item level or if there is a need to override it dynamically via the Json Payload.
                var scheduledPublishTime = outboxItem.ScheduledPublishDateTimeUtc
                    ?? GetJsonValueSafely<DateTimeOffset?>(json, JsonMessageFields.ScheduledPublishDateTimeUtc)
                    ?? GetJsonValueSafely<DateTimeOffset?>(json, JsonMessageFields.ScheduledPublishTime);

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
                    Subject = GetJsonValueSafely<string>(json, JsonMessageFields.Subject) ?? GetJsonValueSafely(json, JsonMessageFields.Label, defaultLabel),
                    //Scheduled Publish time is nullable, so when setting it we coalesce to default if null
                    //  which matches the out-of-the-box behavior of Azure Service Bus.
                    //NOTE: Any value in the past, including `default`, will be treated as "available for immediate delivery" by Azure Service Bus,
                    //      so there is no risk of setting a past time here, or `default` if our value is null.
                    ScheduledEnqueueTime = scheduledPublishTime ?? default
                };

                //Initialize the Body from dynamic Json if defined, or fallback to the entire body...
                var messageBody = GetJsonValueSafely(json, JsonMessageFields.Body, outboxItem.Payload);
                message.Body = new BinaryData(ConvertPublishingPayloadToBytes(messageBody));

                //Populate HeadersLookup/User Properties if defined dynamically...
                var headers = json[JsonMessageFields.Headers]
                    ?? json[JsonMessageFields.AppProperties]
                    ?? json[JsonMessageFields.UserProperties];

                if (headers is JsonObject jsonHeaders)
                    foreach (var propName in jsonHeaders.GetPropertyNames())
                        if(jsonHeaders.PropertyValueSafely<string>(propName) is string stringValue)
                            message.ApplicationProperties.TryAdd(MessageHeaders.ToHeader(propName.ToLower()), stringValue);

            }
            else //Process the payload as a raw string with no additional dynamic fields...
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

            //Derive the actual scheduled publish delay (as a TimeSpan) for informational/logging purposes and for flexible processing in case this is helpful.
            var scheduledDelayTimeSpan = outboxItem.ScheduledPublishDateTimeUtc != null
                ? outboxItem.ScheduledPublishDateTimeUtc.Value - DateTimeOffset.UtcNow
                : (TimeSpan?)null;

            //Add all default headers/user-properties...
            var messageProps = message.ApplicationProperties;
            messageProps.TryAdd(MessageHeaders.ProcessorType, nameof(SqlTransactionalOutbox));
            messageProps.TryAdd(MessageHeaders.ProcessorSender, this.SenderApplicationName);
            messageProps.TryAdd(MessageHeaders.OutboxUniqueIdentifier, uniqueIdString);
            //NOTE: Azure automatically handles serialization/de-serailization of custom message properties
            //  so we don't need to do anything special here -- for DateTimeOffset values, Integers, etc.
            messageProps.TryAdd(MessageHeaders.OutboxCreatedDateUtc, outboxItem.CreatedDateTimeUtc);
            messageProps.TryAdd(MessageHeaders.OutboxScheduledPublishDateUtc, outboxItem.ScheduledPublishDateTimeUtc);
            messageProps.TryAdd(MessageHeaders.OutboxScheduledPublishDelayTimeSpan, scheduledDelayTimeSpan);
            messageProps.TryAdd(MessageHeaders.OutboxPublishingAttempts, outboxItem.PublishAttempts);
            messageProps.TryAdd(MessageHeaders.OutboxPublishingTarget, outboxItem.PublishTarget);
            
            return message;
        }

        protected virtual string ConvertUniqueIdentifierToString(TUniqueIdentifier uniqueIdentifier)
            => uniqueIdentifier.ToString();

        protected virtual byte[] ConvertPublishingPayloadToBytes(string publishingPayload)
            => Encoding.UTF8.GetBytes(publishingPayload);

        protected virtual TValue GetJsonValueSafely<TValue>(JsonObject json, string fieldName, TValue defaultValue = default)
            => json.PropertyValueSafely(fieldName, defaultValue);

        protected virtual JsonObject ParsePayloadAsJsonSafely(ISqlTransactionalOutboxItem<TUniqueIdentifier> outboxItem)
        {
            try
            {
                var json = outboxItem.Payload.FromJsonTo<JsonObject>();
                return json;
            }
            catch (Exception exc)
            {
                if (Options.ThrowExceptionOnJsonPayloadParseFailure)
                {
                    throw new ArgumentException(
                        $"Json parsing failure; the publishing payload for item [{outboxItem.UniqueIdentifier}] could not be be parsed as Json.", 
                        exc
                    );
                }

                return null;
            }
        }
    }
}
