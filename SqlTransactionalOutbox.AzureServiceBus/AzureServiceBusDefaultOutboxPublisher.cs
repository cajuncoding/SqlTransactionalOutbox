using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class AzureServiceBusDefaultOutboxPublisher : AzureServiceBusPublisher<Guid>
    {
        public AzureServiceBusDefaultOutboxPublisher(
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
