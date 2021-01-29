using System;
using System.Collections.Generic;
using System.Text;
using SqlTransactionalOutbox.AzureServiceBus.Receiving;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class DefaultFifoAzureServiceBusReceiver<TPayload>: AzureServiceBusReceiver<Guid, TPayload>
    {
        public DefaultFifoAzureServiceBusReceiver(
            string azureServiceBusConnectionString, 
            ISqlTransactionalOutboxItemFactory<Guid, TPayload> outboxItemFactory = null,
            AzureServiceBusReceivingOptions options = null
        ) 
        : base(
            azureServiceBusConnectionString, 
            outboxItemFactory ?? new OutboxItemFactory<Guid, TPayload>(new OutboxGuidUniqueIdentifier()),
            options ?? new AzureServiceBusReceivingOptions() { FifoEnforcedReceivingEnabled = true }
        )
        {
        }
    }
}
