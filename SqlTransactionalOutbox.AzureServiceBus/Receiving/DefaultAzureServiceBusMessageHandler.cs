using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class DefaultAzureServiceBusMessageHandler<TPayload> : AzureServiceBusMessageHandler<Guid, TPayload>
    {
        public DefaultAzureServiceBusMessageHandler(Message azureServiceBusMessage)
        : base(
            azureServiceBusMessage,
            new DefaultOutboxItemFactory<TPayload>(),
            azureServiceBusClient: null
        )
        {
        }

        public DefaultAzureServiceBusMessageHandler(
            Message azureServiceBusMessage, 
            ISqlTransactionalOutboxItemFactory<Guid, TPayload> outboxItemFactory = null, 
            IReceiverClient azureServiceBusClient = null
        ) 
        : base(
            azureServiceBusMessage, 
            outboxItemFactory ?? new DefaultOutboxItemFactory<TPayload>(), 
            azureServiceBusClient
        )
        {
        }
    }
}
