using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SqlTransactionalOutbox.AzureServiceBus;
using SqlTransactionalOutbox.SqlServer.MicrosoftDataNS;
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
            var jsonText = await req.ReadAsStringAsync();
            var payloadBuilder = PayloadBuilder.FromJsonSafely(jsonText);

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
                publishTarget: payloadBuilder.PublishTarget,
                payload: payloadBuilder.ToJObject(),
                fifoGroupingIdentifier: payloadBuilder.FifoGroupingId
            ).ConfigureAwait(false);

            //Log results and return response to the client...
            log.LogDebug($"Payload:{Environment.NewLine}{outboxItem.Payload}");
            
            return new ContentResult()
            {
                Content = outboxItem.Payload,
                ContentType = MessageContentTypes.Json,
                StatusCode = (int)HttpStatusCode.OK
            };
        }
        
    }

}
