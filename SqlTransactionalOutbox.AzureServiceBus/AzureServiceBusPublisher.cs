using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Newtonsoft.Json.Linq;


namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class AzureServiceBusPublisher<TUniqueIdentifier> : ISqlTransactionalOutboxPublisher<TUniqueIdentifier>
    {
        public string ConnectionString { get; }
        public AzureServiceBusPublishingOptions Options { get; }
        protected AzureSenderClientCache SenderClientCache { get; }
        public string SenderApplicationName { get; protected set; }

        public AzureServiceBusPublisher(
            string azureServiceBusConnectionString,
            AzureServiceBusPublishingOptions options = null
        )
        {
            this.Options = options ?? new AzureServiceBusPublishingOptions();

            this.ConnectionString = azureServiceBusConnectionString;
            this.SenderClientCache = new AzureSenderClientCache(CreateSenderClient);
            this.SenderApplicationName = Options.SenderApplicationName;
        }

        public async Task PublishOutboxItemAsync(ISqlTransactionalOutboxItem<TUniqueIdentifier> outboxItem)
        {
            var message = CreateEventBusMessage(outboxItem);

            Options.LogDebugCallback?.Invoke($"Initializing Sender Client for Topic [{outboxItem.PublishingTarget}]...");
            var senderClient = SenderClientCache.InitializeSenderClient(outboxItem.PublishingTarget);

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
                Options.LogDebugCallback?.Invoke($"PublishingPayload for [{uniqueIdString}] is valid Json; initializing dynamic Message Parameters from the Json root properties...");

                message = new Message
                {
                    MessageId = uniqueIdString,
                    CorrelationId = string.Empty,
                    To = GetJsonValueSafely(json, "to", string.Empty),
                    ReplyTo = GetJsonValueSafely(json, "replyTo", string.Empty),
                    ReplyToSessionId = GetJsonValueSafely(json, "replyToSessionId", string.Empty),
                    PartitionKey = GetJsonValueSafely(json, "partitionKey", string.Empty),
                    ViaPartitionKey = GetJsonValueSafely(json, "viaPartitionKey", string.Empty),
                    ContentType = GetJsonValueSafely(json, "contentType", MessageContentTypes.Json),
                    Label = GetJsonValueSafely(json, "label", defaultLabel)
                };

                //Initialize the Body from dynamic Json if defined, or fallback to the entire body...
                var messageBody = GetJsonValueSafely(json, "body", outboxItem.PublishingPayload);
                message.Body = ConvertPublishingPayloadToBytes(messageBody);

                //Populate Headers/User Properties if defined dynamically...
                var headers = GetJsonValueSafely<JObject>(json, "headers", null)
                              ?? GetJsonValueSafely<JObject>(json, "userProperties", null);

                if (headers != null)
                {
                    foreach (JProperty prop in headers.Properties())
                    {
                        message.UserProperties.Add(prop.Name, prop.Value<object>());
                    }
                }

            }
            else
            //Process as a string with no additional dynamic fields...
            {
                Options.LogDebugCallback?.Invoke($"PublishingPayload for [{uniqueIdString}] is is plain text.");

                message = new Message
                {
                    MessageId = uniqueIdString,
                    CorrelationId = string.Empty,
                    Label = defaultLabel,
                    ContentType = MessageContentTypes.PlainText,
                    Body = ConvertPublishingPayloadToBytes(outboxItem.PublishingPayload)
                };
            }

            //Add all default headers/user-properties...
            //TODO: Make these Header/Prop names constants...
            message.UserProperties.Add("outbox-processor-type", nameof(SqlTransactionalOutbox));
            message.UserProperties.Add("outbox-processor-sender", this.SenderApplicationName);
            message.UserProperties.Add("outbox-item-unique-identifier", uniqueIdString);
            message.UserProperties.Add("outbox-item-created-date-utc", outboxItem.CreatedDateTimeUtc);
            message.UserProperties.Add("outbox-item-publishing-attempts", outboxItem.PublishingAttempts);
            message.UserProperties.Add("outbox-item-publishing-target", outboxItem.PublishingTarget);
            
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
                var json = JObject.Parse(outboxItem.PublishingPayload);
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
