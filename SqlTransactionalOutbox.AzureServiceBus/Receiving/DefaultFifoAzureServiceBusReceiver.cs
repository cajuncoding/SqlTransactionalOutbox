using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class DefaultFifoAzureServiceBusReceiver<TPayload>: AzureServiceBusReceiver<Guid, TPayload>
    {
        public DefaultFifoAzureServiceBusReceiver(
            string azureServiceBusConnectionString, 
            ISqlTransactionalOutboxItemFactory<Guid, TPayload> outboxItemFactory = null
        ) 
        : base(
            azureServiceBusConnectionString, 
            outboxItemFactory ?? new OutboxItemFactory<Guid, TPayload>(new OutboxGuidUniqueIdentifier()),
            enableFifoEnforcedReceiving: true
        )
        {
        }
    }
}
