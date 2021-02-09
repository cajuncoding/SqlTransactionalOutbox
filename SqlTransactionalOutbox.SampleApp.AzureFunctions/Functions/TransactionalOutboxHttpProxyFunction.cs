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
            var outboxItem = await sqlConnection.AddTransactionalOutboxPendingItemAsync(
                publishTopic: payloadBuilder.PublishTopic,
                payload: payloadBuilder.ToJObject(),
                fifoGroupingIdentifier: payloadBuilder.FifoGroupingId
            ).ConfigureAwait(false);

            //************************************************************
            //*** Immediately attempt to process the Outbox...
            //************************************************************
            await sqlConnection
                .ProcessPendingOutboxItemsAsync(
                    outboxPublisher: GetAzureServiceBusPublisher(log),
                    processingOptions: GetOutboxProcessingOptions(log)
                )
                .ConfigureAwait(false);

            
            //Log results and return response to the client...
            log.LogDebug($"Payload:{Environment.NewLine}{outboxItem.Payload}");
            
            return new ContentResult()
            {
                Content = outboxItem.Payload,
                ContentType = MessageContentTypes.Json,
                StatusCode = (int)HttpStatusCode.OK
            };
        }

        public virtual DefaultAzureServiceBusOutboxPublisher GetAzureServiceBusPublisher(ILogger log)
        {
            return new DefaultAzureServiceBusOutboxPublisher(
                FunctionsConfiguration.AzureServiceBusConnectionString,
                new AzureServiceBusPublishingOptions()
                {
                    SenderApplicationName = $"[{typeof(TransactionalOutboxHttpProxyFunction).Assembly.GetName().Name}]",
                    LogDebugCallback = (s) => log.LogDebug(s),
                    LogErrorCallback = (e) => log.LogError(e, "Unexpected Exception occurred while attempting to store into the Transactional Outbox.")
                }
            );
        }

        public virtual OutboxProcessingOptions GetOutboxProcessingOptions(ILogger log)
        {
            return new OutboxProcessingOptions()
            {
                ItemProcessingBatchSize = 10, //Only process the top 10 items to keep this function responsive!
                FifoEnforcedPublishingEnabled = true,
                LogDebugCallback = (s) => log.LogDebug(s),
                LogErrorCallback = (e) => log.LogError(e, "Unexpected Exception occurred while Processing Items from the Transactional Outbox."),
                MaxPublishingAttempts = FunctionsConfiguration.OutboxMaxPublishingRetryAttempts,
                TimeSpanToLive = FunctionsConfiguration.OutboxMaxTimeToLiveDays
            };
        }
        
    }

}
