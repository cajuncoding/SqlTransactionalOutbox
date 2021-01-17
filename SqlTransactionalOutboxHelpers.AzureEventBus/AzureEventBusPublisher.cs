using System;
using System.Threading.Tasks;


namespace SqlTransactionalOutboxHelpers.AzureEventBus
{
    public class AzureEventBusPublisher : ISqlTransactionalOutboxPublisher<Guid>
    {
        public Task PublishOutboxItemAsync(ISqlTransactionalOutboxItem<Guid> outboxItem)
        {
            throw new NotImplementedException();
        }
    }
}
