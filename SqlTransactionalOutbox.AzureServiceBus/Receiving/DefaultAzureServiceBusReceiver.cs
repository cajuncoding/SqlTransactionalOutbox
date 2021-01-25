using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class DefaultAzureServiceBusReceiver<TPayload>: AzureServiceBusReceiver<Guid, TPayload>
    {
        public DefaultAzureServiceBusReceiver(
            string azureServiceBusConnectionString, 
            ISqlTransactionalOutboxItemFactory<Guid, TPayload> outboxItemFactory = null) 
        : base(
            azureServiceBusConnectionString, 
            outboxItemFactory ?? new OutboxItemFactory<Guid, TPayload>(new OutboxGuidUniqueIdentifier())
        )
        {
        }
    }
}
