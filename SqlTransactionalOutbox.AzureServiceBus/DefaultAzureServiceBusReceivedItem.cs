using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using SqlTransactionalOutbox.AzureServiceBus.Receiving;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class DefaultAzureServiceBusReceivedItem<TPayload> : AzureServiceBusReceivedItem<Guid, TPayload>
    {
        public DefaultAzureServiceBusReceivedItem(
            Message azureServiceBusMessage,
            ISqlTransactionalOutboxItemFactory<Guid, TPayload> outboxItemFactory = null,
            //Client is OPTIONAL; necessary when processing will be handled by AzureFunctions framework bindings, etc.
            IReceiverClient azureServiceBusClient = null,
            bool isFifoProcessingEnabled = false
        )
        : base(
            azureServiceBusMessage: azureServiceBusMessage, 
            outboxItemFactory: outboxItemFactory ?? new DefaultOutboxItemFactory<TPayload>(),
            azureServiceBusClient: azureServiceBusClient,
            isFifoProcessingEnabled: isFifoProcessingEnabled
        )
        {
        }
    }

}
