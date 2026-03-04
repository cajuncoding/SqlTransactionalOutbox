using System;
using SqlTransactionalOutbox.AzureServiceBus.Publishing;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class DefaultAzureServiceBusOutboxPublisher : BaseAzureServiceBusPublisher<Guid>
    {
        public DefaultAzureServiceBusOutboxPublisher(
            string azureServiceBusConnectionString,
            AzureServiceBusPublishingOptions options = null
        )
        : base (
            azureServiceBusConnectionString,
            options
        )
        {
            //All logic is currently in the Base Constructor
        }
    }
}
