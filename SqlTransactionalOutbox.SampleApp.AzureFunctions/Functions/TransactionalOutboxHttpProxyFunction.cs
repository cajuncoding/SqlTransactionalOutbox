using System;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SqlTransactionalOutbox.AzureServiceBus;
using SqlTransactionalOutbox.SqlServer.SystemDataNS;
using SqlTransactionalOutbox.Utilities;

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
            
            //Initialize the Payload from the Body as Json!
            var body = await req.ReadAsStringAsync();
            var payloadBuilder = PayloadBuilder.FromJsonSafely(body);

            //Apply fallback values from the QueryString
            //NOTE: this will only set values not already initialized from Json!
            var queryLookup = req.Query.ToLookup(k => k.Key, v => v.Value.FirstOrDefault());
            payloadBuilder.ApplyValues(queryLookup, false);

            var sqlConnection = new SqlConnection(FunctionsConfiguration.SqlConnectionString);
            await sqlConnection.OpenAsync();

            //************************************************************
            //*** Add The Payload to our Outbox
            //************************************************************
            var publishTopic = payloadBuilder.PublishTopic;
            var jsonPayload = payloadBuilder.ToJObject();

            var outboxItem = await AddPendingItemToOutboxAsync(sqlConnection, publishTopic, jsonPayload).ConfigureAwait(false);

            //************************************************************
            //*** Immediately attempt to process the Outbox...
            //************************************************************
            await ExecuteOutboxProcessingAsync(sqlConnection, log).ConfigureAwait(false);

            
            log.LogDebug($"Payload:{Environment.NewLine}{outboxItem.Payload}");
            
            return new ContentResult()
            {
                Content = outboxItem.Payload,
                ContentType = MessageContentTypes.Json,
                StatusCode = (int)HttpStatusCode.OK
            };
        }

        public async Task<ISqlTransactionalOutboxItem<Guid>> AddPendingItemToOutboxAsync(
            SqlConnection sqlConnection, 
            string publishTopic,
            JObject jsonPayload
        )
        {
            await using var outboxTransaction = (SqlTransaction)(await sqlConnection.BeginTransactionAsync());
            try
            {
                //SAVE the Item to the Outbox...
                var outbox = new DefaultSqlServerTransactionalOutbox<JObject>(outboxTransaction);
                var outboxItem = await outbox.InsertNewPendingOutboxItemAsync(publishTopic, jsonPayload).ConfigureAwait(false);

                await outboxTransaction.CommitAsync().ConfigureAwait(false);
                return outboxItem;
            }
            catch(Exception)
            {
                await outboxTransaction.RollbackAsync().ConfigureAwait(false);
                throw;
            }
        }

        public async Task ExecuteOutboxProcessingAsync(SqlConnection sqlConnection, ILogger log)
        {
            //PROCESS Pending Items in the Outbox...
            await using var outboxProcessingTransaction = (SqlTransaction)(await sqlConnection.BeginTransactionAsync());

            try
            {
                var outboxProcessor = new DefaultSqlServerTransactionalOutboxProcessor<object>(
                    outboxProcessingTransaction,
                    new DefaultAzureServiceBusOutboxPublisher(
                        FunctionsConfiguration.AzureServiceBusConnectionString,
                        new AzureServiceBusPublishingOptions()
                        {
                            SenderApplicationName = $"[{typeof(TransactionalOutboxHttpProxyFunction).Assembly.GetName().Name}]",
                            LogDebugCallback = (s) => log.LogDebug(s),
                            LogErrorCallback = (e) => log.LogError(e, "Unexpected Exception occurred while attempting to store into the Transactional Outbox.")
                        }
                    )
                );

                await outboxProcessor.ProcessPendingOutboxItemsAsync(
                    new OutboxProcessingOptions()
                    {
                        ItemProcessingBatchSize = 10,  //Only process the top 10 items to keep this function responsive!
                        FifoEnforcedPublishingEnabled = true,
                        LogDebugCallback = (s) => log.LogDebug(s),
                        LogErrorCallback = (e) => log.LogError(e, "Unexpected Exception occurred while Processing Items from the Transactional Outbox."),
                        //TODO: Make Configuration Options!
                        MaxPublishingAttempts = 50,
                        TimeSpanToLive = TimeSpan.FromHours(24)
                    }
                );

                await outboxProcessingTransaction.CommitAsync().ConfigureAwait(false);
            }
            catch(Exception)
            {
                await outboxProcessingTransaction.RollbackAsync().ConfigureAwait(false);
                throw;
            }
        }

        ////TODO: Move to Re-Useable Element for Http Azure Functions Proxying...
        //public string GetValueFromRequest(string field, JObject json, ILookup<string, string> query, string defaultValue = null, bool isRequired = false)
        //{
        //    var value = json.ValueSafely<string>(field) ?? query[field].FirstOrDefault() ?? defaultValue;

        //    if(value == null && isRequired)
        //        throw new ArgumentNullException($"The field [{field}] is required but no value for [{field}] could be found on the request querystring or in the body payload.");

        //    return value;
        //}

        //public string GetContentType(JObject json, ILookup<string, string> query)
        //{
        //    //First check the Body & Query to see if ContentType is specified!
        //    var contentType = GetValueFromRequest("ContentType", json, query);

        //    //If not then dynamically determine by inspection...
        //    if (string.IsNullOrWhiteSpace(contentType))
        //    {
        //        var payloadBody = GetValueFromRequest("Body", json, query);

        //        //Content type is Json if the specified payload is valid Json or if the fallback to the original request payload
        //        //  is valid json (which we know if the JObject isn't null).
        //        var isValidJson = (!string.IsNullOrWhiteSpace(payloadBody) && JsonHelpers.IsValidJson(payloadBody)) //Specified Body is Json?
        //                          || (json != null); //Original Payload is Json?

        //        contentType = isValidJson ? MessageContentTypes.Json : MessageContentTypes.PlainText;
        //    }

        //    return contentType;
        //}
    }

}
