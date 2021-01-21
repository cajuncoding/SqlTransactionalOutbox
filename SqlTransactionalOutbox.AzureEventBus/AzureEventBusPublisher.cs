using System;
using System.Threading.Tasks;


namespace SqlTransactionalOutbox.AzureEventBus
{
    public class AzureEventBusPublisher : ISqlTransactionalOutboxPublisher<Guid>
    {
        public Task PublishOutboxItemAsync(ISqlTransactionalOutboxItem<Guid> outboxItem)
        {
            throw new NotImplementedException();
        }
    }
}
