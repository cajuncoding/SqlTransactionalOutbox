using System;
using System.Collections.Generic;
using System.Text;

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
        }
}
}
