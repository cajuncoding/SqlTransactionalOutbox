using System;
using System.Collections.Generic;
using System.Text;
using SqlTransactionalOutbox.AzureServiceBus.Receiving;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class DefaultAzureServiceBusReceiver<TPayload>: AzureServiceBusReceiver<Guid, TPayload>
    {
        public DefaultAzureServiceBusReceiver(
            string azureServiceBusConnectionString, 
            ISqlTransactionalOutboxItemFactory<Guid, TPayload> outboxItemFactory = null,
            AzureServiceBusReceivingOptions options = null
        )
        : base(
            azureServiceBusConnectionString, 
            outboxItemFactory ?? new OutboxItemFactory<Guid, TPayload>(new OutboxGuidUniqueIdentifier()),
            options
        )
        {
        }
    }
}
