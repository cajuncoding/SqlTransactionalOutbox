using System;
using Microsoft.Azure.ServiceBus;
using SqlTransactionalOutbox.AzureServiceBus.Receiving;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public static class AzureServiceBusCustomExtensions
    {
        /// <summary>
        /// Convert the Azure Service Bus Message into a Transactional Outbox Received item.
        /// This will re-hydrate all outbox custom fields/data (e.g. headers) into properties for
        /// easier access and processing with the Transactional Outbox library.
        /// </summary>
        /// <typeparam name="TParseBody"></typeparam>
        /// <param name="serviceBusMessage"></param>
        /// <returns></returns>
        public static AzureServiceBusReceivedItem<Guid, TParseBody> ToOutboxReceivedItem<TParseBody>(
            this Message serviceBusMessage
        )
        {
            var receivedItem = new DefaultAzureServiceBusReceivedItem<TParseBody>(serviceBusMessage);
            return receivedItem;
        }
    }
}
