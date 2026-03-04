using Microsoft.Data.SqlClient;
using SqlTransactionalOutbox.SampleApp.AzureFunctions;
using SqlTransactionalOutbox.SqlServer.MicrosoftDataNS;
using SqlTransactionalOutbox.Utilities;

namespace SqlTransactionalOutbox.SampleApp.ConsoleApp
{
    /// <summary>
    ///******************************************************************************************
    /// 1. SENDING Messages via the Sql Transactional Outbox
    /// 
    ///  We Need a Payload Sender to populate the Outbox with messages/payloads...
    ///  NOTE: this is a wrapper class to simplify this code and encapsulate Connection Handling!
    ///******************************************************************************************
    /// </summary>
    public class OutboxSender
    {
        public string ServiceBusTopic { get; }

        public string SqlConnectionString { get; set; }

        public OutboxSender(SampleAppConfig settings)
        {
            this.ServiceBusTopic = settings.AzureServiceBusTopic;
            this.SqlConnectionString = settings.SqlConnectionString;
        }

        public async Task<ISqlTransactionalOutboxItem<Guid>> SendMessageAsync(string message, TimeSpan? scheduledDelayTime)
        {
            //Initialize the Payload from the Body as Json!
            var payloadBuilder = new PayloadBuilder()
            {
                PublishTarget= this.ServiceBusTopic,
                To = "CajunCoding",
                Body = message,
                FifoGroupingId = "AllConsoleAppTestItemsShouldBeFIFO",
                ScheduledPublishDateTimeUtc = scheduledDelayTime.HasValue
                    ? DateTimeOffset.UtcNow.Add(scheduledDelayTime.Value)
                    : null
            };

            await using var sqlConnection = new SqlConnection(this.SqlConnectionString);
            await sqlConnection.OpenAsync();

            //************************************************************
            //*** Add The Payload to our Outbox
            //************************************************************
            var outboxItem = await sqlConnection.AddTransactionalOutboxPendingItemAsync(
                publishTarget: payloadBuilder.PublishTarget,
                payload: payloadBuilder.ToJObject()
            ).ConfigureAwait(false);

            return outboxItem;
        }
    }
}
