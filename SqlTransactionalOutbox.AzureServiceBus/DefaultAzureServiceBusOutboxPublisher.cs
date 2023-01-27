using System;
using System.Collections.Generic;
using System.Text;
using Azure.Messaging.ServiceBus;
using SqlTransactionalOutbox.AzureServiceBus.Publishing;
using SqlTransactionalOutbox.Publishing;

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
