using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Functions.Worker.AddOns.Common;
using SqlTransactionalOutbox.SqlServer.MicrosoftDataNS;
using SqlTransactionalOutbox.Utilities;
using SystemTextJsonHelpers;

namespace SqlTransactionalOutbox.SampleApp.AzureFunctions
{
    public class TransactionalOutboxHttpProxySendPayloadFunction
    {
        //******************************************************************************************
        // 1. SENDING Messages via the Sql Transactional Outbox
        //******************************************************************************************
        [Function(nameof(TransactionalOutboxHttpProxySendPayloadFunction))]
        public async Task<PayloadBuilder> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "send-payload")] HttpRequestData req,
            FunctionContext functionContext
        )
        {
            var logger = functionContext.GetLogger();
            logger.LogInformation($"HTTP [{nameof(TransactionalOutboxHttpProxySendPayloadFunction)}].");
            logger.LogInformation($"SENDING EVENT at [{DateTime.Now}]...");

            var configSettings = new SampleAppConfig();

            //Initialize the Payload from the Body as Json!
            //NOTE: This allows for easy dynamic population of various Outbox/Payload/Processing properties
            //     such as PublishTarget, ScheduledPublishDateTimeUtc, FifoGroupingId, etc. directly from the
            //     JSON Payload which provides full flexibility and control to the clients sending the messages!
            var jsonText = await req.ReadAsStringAsync();
            var payloadBuilder = PayloadBuilder.FromJson(jsonText);

            //Apply fallback values from the QueryString along with all other binding values from the Function Binding Data!
            //NOTE: This will only set values not already initialized from Json above...
            var bindingData = req.FunctionContext.BindingContext.BindingData;
            if (bindingData.Count > 0)
            {
                var bindingDataLookup = bindingData.ToLookup(k => k.Key, v => v.Value?.ToString(), StringComparer.OrdinalIgnoreCase);
                payloadBuilder.ApplyValues(bindingDataLookup, false);
            }

            await using var sqlConnection = new SqlConnection(configSettings.SqlConnectionString);
            await sqlConnection.OpenAsync();

            //************************************************************
            //*** Add The Payload to our Outbox
            //************************************************************
            var jsonPayload = payloadBuilder.ToJsonObject();

            var outboxItem = await sqlConnection.AddTransactionalOutboxPendingItemAsync(
                publishTarget: payloadBuilder.PublishTarget,
                payload: jsonPayload,
                //It's always a good idea to ensure that a FIFO Group Id/Name is specified for any FIFO Subscriptions that may receive the messages...
                fifoGroupingIdentifier: payloadBuilder.FifoGroupingId ?? "DefaultFifoGroup"
            ).ConfigureAwait(false);

            //Log results and return response to the client...
            logger.LogInformation($"Payload:{Environment.NewLine}{jsonPayload.ToJsonIndented()}");

            return payloadBuilder;
        }
    }
}
