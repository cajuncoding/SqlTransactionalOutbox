using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SqlTransactionalOutbox.SampleApp.AzureFunctions;
using SqlTransactionalOutbox.SqlServer.MicrosoftDataNS;
using SqlTransactionalOutbox.Utilities;

namespace SqlTransactionalOutbox.SampleApp.ConsoleApp
{
    public class OutboxSender
    {
        public string ServiceBusTopic { get; }

        public string SqlConnectionString { get; set; }

        public OutboxSender(string topic, string sqlConnectionString)
        {
            this.ServiceBusTopic = topic;
            this.SqlConnectionString = sqlConnectionString;
        }

        public async Task<ISqlTransactionalOutboxItem<Guid>> SendMessageAsync(string message)
        {
            //Initialize the Payload from the Body as Json!
            var payloadBuilder = new PayloadBuilder()
            {
                PublishTarget= this.ServiceBusTopic,
                To = "CajunCoding",
                Body = message,
                FifoGroupingId = "AllConsoleAppTestItemsShouldBeFIFO"
            };

            await using var sqlConnection = new SqlConnection(this.SqlConnectionString);
            await sqlConnection.OpenAsync();

            //************************************************************
            //*** Add The Payload to our Outbox
            //************************************************************
            var outboxItem = await sqlConnection.AddTransactionalOutboxPendingItemAsync(
                publishTarget: payloadBuilder.PublishTarget,
                payload: payloadBuilder.ToJObject(),
                //It's always a good idea to ensure that a FIFO Group Id/Name is specified for any FIFO Subscriptions that may receive the messages...
                fifoGroupingIdentifier: payloadBuilder.FifoGroupingId ?? "DefaultFifoGroup"
            ).ConfigureAwait(false);

            return outboxItem;
        }
    }
}
