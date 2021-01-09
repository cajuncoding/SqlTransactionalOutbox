using System;
using System.Threading.Tasks;


namespace SqlTransactionalOutboxHelpers.AzureEventBus
{
    public class AzureEventBusPublisher : ISqlTransactionalOutboxPublisher
    {
        public Task PublishOutboxItemAsync(OutboxItem outboxItem)
        {
            throw new NotImplementedException();
        }
    }
}
