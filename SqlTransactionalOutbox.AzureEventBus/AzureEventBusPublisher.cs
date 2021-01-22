using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Newtonsoft.Json.Linq;


namespace SqlTransactionalOutbox.AzureEventBus
{
    public class AzureEventBusPublisher<TUniqueIdentifier> : ISqlTransactionalOutboxPublisher<TUniqueIdentifier>
    {
        public string ConnectionString { get; protected set; }
        public AzureEventBusPublishingOptions Options { get; protected set; }

        protected ConcurrentDictionary<string, Lazy<ISenderClient>> SenderClients { get; } = new ConcurrentDictionary<string, Lazy<ISenderClient>>();

        public AzureEventBusPublisher(
            string azureEventBusConnectionString,
            AzureEventBusPublishingOptions options = null
        )
        {
            this.ConnectionString = azureEventBusConnectionString;
            this.Options = options ?? new AzureEventBusPublishingOptions();
        }

        public async Task PublishOutboxItemAsync(ISqlTransactionalOutboxItem<TUniqueIdentifier> outboxItem)
        {
            var message = CreateEventBusMessage(outboxItem);

            //Get the correct Sender Client (for Azure Service Bus that will be an ITopicClient)
            //  and send the well-formed message...
            var senderClient = InitializeSenderClient(outboxItem.PublishingTarget);
            await senderClient.SendAsync(message);

            Options.LogDebugCallback?.Invoke($"Azure Service Bus message [{message.Label}] has been published.");
        }

        protected virtual ISenderClient InitializeSenderClient(string topic)
        {
            var senderClientLazy = SenderClients.GetOrAdd(topic, new Lazy<ISenderClient>(() =>
                {
                    Options.LogDebugCallback?.Invoke($"New Sender Client has been created for Topic [{topic}]!");
                    return CreateSenderClient(topic);
                })
            );

            var senderClient = senderClientLazy.Value;
            return senderClient;
        }

        protected virtual ISenderClient CreateSenderClient(string publishingTarget)
        {
            var senderClient = new TopicClient(this.ConnectionString, publishingTarget, Options.RetryPolicy);
            return senderClient;
        }

        protected virtual Message CreateEventBusMessage(ISqlTransactionalOutboxItem<TUniqueIdentifier> outboxItem)
        {
            var uniqueIdString = ConvertUniqueIdentifierToString(outboxItem.UniqueIdentifier);
            var defaultLabel = $"[PublishedBy={Options.DefaultMessageLabelPrefix}][MessageId={uniqueIdString}]";
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
                    ContentType = GetJsonValueSafely(json, "contentType", "application/json;charset=utf-8"),
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
                    ContentType = "text/plain;charset=utf-8",
                    Body = ConvertPublishingPayloadToBytes(outboxItem.PublishingPayload)
                };
            }

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
