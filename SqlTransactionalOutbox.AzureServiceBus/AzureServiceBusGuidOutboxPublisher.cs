using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class AzureServiceBusGuidOutboxPublisher : AzureServiceBusPublisher<Guid>
    {
        public AzureServiceBusGuidOutboxPublisher(
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
