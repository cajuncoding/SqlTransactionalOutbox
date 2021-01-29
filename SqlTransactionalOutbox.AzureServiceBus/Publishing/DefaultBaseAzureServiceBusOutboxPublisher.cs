using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class DefaultBaseAzureServiceBusOutboxPublisher : BaseAzureServiceBusPublisher<Guid>
    {
        public DefaultBaseAzureServiceBusOutboxPublisher(
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
