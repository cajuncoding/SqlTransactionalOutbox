using System;
using System.Collections.Generic;
using System.Text;
using Azure.Messaging.ServiceBus;
using SqlTransactionalOutbox.AzureServiceBus.Receiving;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class DefaultAzureServiceBusReceivedItem<TPayloadBody> : AzureServiceBusReceivedItem<Guid, TPayloadBody>
    {
        public DefaultAzureServiceBusReceivedItem(
            ServiceBusReceivedMessage azureServiceBusMessage,
            ISqlTransactionalOutboxItemFactory<Guid, TPayloadBody> outboxItemFactory = null,
            //Client is OPTIONAL; necessary when processing will be handled by AzureFunctions framework bindings, etc.
            ServiceBusReceiver azureServiceBusReceiverClient = null
        )
        : base(
            azureServiceBusMessage: azureServiceBusMessage, 
            outboxItemFactory: outboxItemFactory ?? new DefaultOutboxItemFactory<TPayloadBody>(),
            azureServiceBusReceiverClient: azureServiceBusReceiverClient
        )
        { }
    }



}
