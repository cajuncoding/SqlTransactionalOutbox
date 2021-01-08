using System;
using System.Threading.Tasks;


namespace SqlTransactionalOutboxHelpers.AzureEventBus
{
    public class AzureEventBusPublisher : ISqlTransactionalOutboxPublisher
    {
        public Task<OutboxPublishingResults> ExecutePublishingProcess(ISqlTransactionalOutboxProcessor outboxProcessor)
        {
            throw new NotImplementedException();
        }
    }
}
