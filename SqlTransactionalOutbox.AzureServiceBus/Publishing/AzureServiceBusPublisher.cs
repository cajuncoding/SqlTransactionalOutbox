﻿using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Newtonsoft.Json.Linq;
using SqlTransactionalOutbox.Interfaces;
using SqlTransactionalOutbox.Publishing;


namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class AzureServiceBusPublisher<TUniqueIdentifier> : ISqlTransactionalOutboxPublisher<TUniqueIdentifier>
    {
        public string ConnectionString { get; }
        public AzureServiceBusPublishingOptions Options { get; }
        public string SenderApplicationName { get; protected set; }
        protected AzureSenderClientCache SenderClientCache { get; }

        public AzureServiceBusPublisher(
            string azureServiceBusConnectionString,
            AzureServiceBusPublishingOptions options = null
        )
        {
            this.Options = options ?? new AzureServiceBusPublishingOptions();

            this.ConnectionString = azureServiceBusConnectionString;
            this.SenderApplicationName = Options.SenderApplicationName;
            this.SenderClientCache = new AzureSenderClientCache();
        }

        public async Task PublishOutboxItemAsync(ISqlTransactionalOutboxItem<TUniqueIdentifier> outboxItem)
        {
            var message = CreateEventBusMessage(outboxItem);

            Options.LogDebugCallback?.Invoke($"Initializing Sender Client for Topic [{outboxItem.PublishTarget}]...");
            var senderClient = SenderClientCache.InitializeClient(
                publishingTarget: outboxItem.PublishTarget, 
                newSenderClientFactory: () => CreateSenderClient(outboxItem.PublishTarget)
            );

            var uniqueIdString = ConvertUniqueIdentifierToString(outboxItem.UniqueIdentifier);
            Options.LogDebugCallback?.Invoke($"Sending the Message [{message.Label}] for outbox item [{uniqueIdString}]...");

            await senderClient.SendAsync(message);

            Options.LogDebugCallback?.Invoke($"Azure Service Bus message [{message.Label}] has been published successfully.");
        }

        protected virtual ISenderClient CreateSenderClient(string publishingTarget)
        {
            var senderClient = new TopicClient(this.ConnectionString, publishingTarget, Options.RetryPolicy);
            return senderClient;
        }

        protected virtual Message CreateEventBusMessage(ISqlTransactionalOutboxItem<TUniqueIdentifier> outboxItem)
        {
            var uniqueIdString = ConvertUniqueIdentifierToString(outboxItem.UniqueIdentifier);
            var defaultLabel = $"[PublishedBy={Options.SenderApplicationName}][MessageId={uniqueIdString}]";
            Message message = null;

            Options.LogDebugCallback?.Invoke($"Creating Azure Message Object from [{uniqueIdString}]...");

            //Attempt to decode teh Message as JObject to see if it contains dynamically defined parameters
            //that need to be mapped into the message; otherwise if it's just a string we populate only the minimum.
            var json = ParsePayloadAsJsonSafely(outboxItem);
            if (json != null)
            {
                Options.LogDebugCallback?.Invoke($"Payload for [{uniqueIdString}] is valid Json; initializing dynamic Message Parameters from the Json root properties...");

                //Get the session id; because it is needed to determine PartitionKey when defined...
                var sessionId = 
                    GetJsonValueSafely(json, "FifoGroupingId", (string) null)
                    ?? GetJsonValueSafely(json, nameof(ISqlTransactionalOutboxPublishedItem<Guid, string>.FifoGroupingIdentifier), (string)null)
                    ?? GetJsonValueSafely(json, "SessionId", (string) null);

                message = new Message
                {
                    MessageId = uniqueIdString,
                    SessionId = sessionId,
                    CorrelationId = GetJsonValueSafely(json, "CorrelationId", string.Empty),
                    To = GetJsonValueSafely(json, "to", string.Empty),
                    ReplyTo = GetJsonValueSafely(json, "replyTo", string.Empty),
                    ReplyToSessionId = GetJsonValueSafely(json, "replyToSessionId", string.Empty),
                    //NOTE: IF SessionId is Defined then the Partition Key MUST MATCH the SessionId...
                    PartitionKey = sessionId ?? GetJsonValueSafely(json, "partitionKey", string.Empty),
                    //Transaction related Partition Key... unused so disabling this to minimize risk.
                    //ViaPartitionKey = sessionId ?? GetJsonValueSafely(json, "viaPartitionKey", string.Empty),
                    ContentType = GetJsonValueSafely(json, "contentType", MessageContentTypes.Json),
                    Label = GetJsonValueSafely(json, "label", defaultLabel)
                };

                //Initialize the Body from dynamic Json if defined, or fallback to the entire body...
                var messageBody = GetJsonValueSafely(json, "body", outboxItem.Payload);
                message.Body = ConvertPublishingPayloadToBytes(messageBody);

                //Populate HeadersLookup/User Properties if defined dynamically...
                var headers = GetJsonValueSafely<JObject>(json, "headers", null)
                              ?? GetJsonValueSafely<JObject>(json, "userProperties", null);

                if (headers != null)
                {
                    foreach (JProperty prop in headers.Properties())
                    {
                        message.UserProperties.Add(
                            MessageHeaders.ToHeader(prop.Name.ToLower()), 
                            prop.Value.ToString()
                        );
                    }
                }

            }
            else
            //Process as a string with no additional dynamic fields...
            {
                Options.LogDebugCallback?.Invoke($"Payload for [{uniqueIdString}] is is plain text.");

                message = new Message
                {
                    MessageId = uniqueIdString,
                    CorrelationId = string.Empty,
                    Label = defaultLabel,
                    ContentType = MessageContentTypes.PlainText,
                    Body = ConvertPublishingPayloadToBytes(outboxItem.Payload)
                };
            }

            //Add all default headers/user-properties...
            //TODO: Make these Header/Prop names constants...
            message.UserProperties.Add(MessageHeaders.ProcessorType, nameof(SqlTransactionalOutbox));
            message.UserProperties.Add(MessageHeaders.ProcessorSender, this.SenderApplicationName);
            message.UserProperties.Add(MessageHeaders.OutboxUniqueIdentifier, uniqueIdString);
            message.UserProperties.Add(MessageHeaders.OutboxCreatedDateUtc, outboxItem.CreatedDateTimeUtc);
            message.UserProperties.Add(MessageHeaders.OutboxPublishingAttempts, outboxItem.PublishAttempts);
            message.UserProperties.Add(MessageHeaders.OutboxPublishingTarget, outboxItem.PublishTarget);
            
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
            var jToken = json.GetValue(fieldName, StringComparison.OrdinalIgnoreCase);
            var value = jToken == null
                ? defaultValue
                : jToken.Value<TValue>();

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
                var argException = new ArgumentException(
                    $"Json parsing failure; the publishing payload for item [{outboxItem.UniqueIdentifier}] could not" +
                    $" be be parsed as Json.", exc
                );

                Options.LogErrorCallback?.Invoke(argException);

                if (Options.ThrowExceptionOnJsonPayloadParseFailure)
                {
                    throw argException;
                }

                return null;
            }
        }

    }
}
