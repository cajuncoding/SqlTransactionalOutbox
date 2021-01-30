using System;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SqlTransactionalOutbox.AzureServiceBus;
using SqlTransactionalOutbox.JsonExtensions;
using SqlTransactionalOutbox.Publishing;
using SqlTransactionalOutbox.SqlServer.SystemDataNS;

namespace SqlTransactionalOutbox.SampleApp.AzureFunctions
{
    public class TransactionalOutboxHttpProxyFunction
    {
        [FunctionName("SendPayload")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation($"HTTP [{nameof(TransactionalOutboxHttpProxyFunction)}].");
            
            var body = await req.ReadAsStringAsync();
            var json = JsonHelpers.ParseSafely(body);
            var queryLookup = req.Query.ToLookup(k => k.Key, v => v.Value.FirstOrDefault());

            var outboxPayload = new
            {
                PublishTopic = GetValueFromRequest("topic", json, queryLookup)
                                ?? GetValueFromRequest("publishTopic", json, queryLookup),
                To = GetValueFromRequest(JsonMessageFields.To, json, queryLookup),
                FifoGroupingId = GetValueFromRequest(JsonMessageFields.FifoGroupingId, json, queryLookup) 
                                 ?? GetValueFromRequest(JsonMessageFields.SessionId, json, queryLookup),
                Label = GetValueFromRequest(JsonMessageFields.Label, json, queryLookup),
                //Fallback to request payload as the Body if no other Body is specified...
                Body = GetValueFromRequest(JsonMessageFields.Body, 
                json, queryLookup) ?? body,
                ContentType = GetContentType(json, queryLookup),
                //Headers are only available via Json Payload as a nested object (Key/Values)...
                Headers = json.ValueSafely<JObject>(JsonMessageFields.Headers)
                            ?? json.ValueSafely<JObject>(JsonMessageFields.UserProperties),
                CorrelationId = GetValueFromRequest(JsonMessageFields.CorrelationId, json, queryLookup),
                ReplyTo = GetValueFromRequest(JsonMessageFields.ReplyTo, json, queryLookup),
                ReplyToSessionId = GetValueFromRequest(JsonMessageFields.ReplyTo, json, queryLookup)
            };
            
            var sqlConnection = new SqlConnection(FunctionsConfiguration.SqlConnectionString);
            await sqlConnection.OpenAsync();

            await using var sqlTransaction = (SqlTransaction)(await sqlConnection.BeginTransactionAsync());

            //SAVE the Item to the Outbox...
            var azureServiceBusPublisher = new DefaultBaseAzureServiceBusOutboxPublisher(
                FunctionsConfiguration.AzureServiceBusConnectionString,
                new AzureServiceBusPublishingOptions()
                {
                    SenderApplicationName = $"[{typeof(TransactionalOutboxHttpProxyFunction).Assembly.GetName().Name}]",
                    LogDebugCallback = (s) => log.LogDebug(s),
                    LogErrorCallback = (e) => log.LogError(e, "Unexpected Exception occurred while attempting to store into the Transactional Outbox.")
                });
            
            var outboxProcessor = new DefaultSqlServerOutboxProcessor<object>(sqlTransaction, azureServiceBusPublisher);
            
            //TODO: Take the Transaction as a Parameter and not a Constructor Injection for better re-use!
            var outboxItem = await outboxProcessor.InsertNewPendingOutboxItemAsync(outboxPayload.PublishTopic, outboxPayload);

            await sqlTransaction.CommitAsync();

            //PROCESS Pending Items in the Outbox...
            await using var sqlTransaction2 = (SqlTransaction)(await sqlConnection.BeginTransactionAsync());
            
            //Wouldn't
            outboxProcessor = new DefaultSqlServerOutboxProcessor<object>(sqlTransaction2, azureServiceBusPublisher);

            var results = await outboxProcessor.ProcessPendingOutboxItemsAsync(
                new OutboxProcessingOptions()
                {
                    ItemProcessingBatchSize = 10,  //Only process the top 10 items to keep this function responsive!
                    EnableDistributedMutexLockForFifoPublishingOrder = true,
                    LogDebugCallback = (s) => log.LogDebug(s),
                    LogErrorCallback = (e) => log.LogError(e, "Unexpected Exception occurred while Processing Items from the Transactional Outbox."),
                    MaxPublishingAttempts = 1,
                    TimeSpanToLive = TimeSpan.FromMinutes(5)
                }
            );

            await sqlTransaction2.CommitAsync();

            //var outboxSerializer = new OutboxPayloadJsonSerializer();
            //var serializedPayload = outboxSerializer.SerializePayload(outboxPayload);
            var serializedPayload = outboxItem.Payload;

            log.LogDebug($"Payload:{Environment.NewLine}{serializedPayload}");

            return new ContentResult()
            {
                Content = serializedPayload,
                ContentType = MessageContentTypes.Json,
                StatusCode = (int)HttpStatusCode.OK
            };
        }

        //TODO: Move to Re-Useable Element for Http Azure Functions Proxying...
        public string GetValueFromRequest(string field, JObject json, ILookup<string, string> query, string defaultValue = default, bool isRequired = false)
        {
            var value = json.ValueSafely<string>(field) ?? query[field].FirstOrDefault();

            if(value == null && isRequired)
                throw new ArgumentNullException($"The field [{field}] is required but no value for [{field}] could be found on the request querystring or in the body payload.");

            return value;
        }

        public string GetContentType(JObject json, ILookup<string, string> query)
        {
            //First check the Body & Query to see if ContentType is specified!
            var contentType = GetValueFromRequest("ContentType", json, query);

            //If not then dynamically determine by inspection...
            if (string.IsNullOrWhiteSpace(contentType))
            {
                var payloadBody = GetValueFromRequest("Body", json, query);

                //Content type is Json if the specified payload is valid Json or if the fallback to the original request payload
                //  is valid json (which we know if the JObject isn't null).
                var isValidJson = (!string.IsNullOrWhiteSpace(payloadBody) && JsonHelpers.IsValidJson(payloadBody)) //Specified Body is Json?
                                  || (json != null); //Original Payload is Json?

                contentType = isValidJson ? MessageContentTypes.Json : MessageContentTypes.PlainText;
            }

            return contentType;
        }
    }

}
