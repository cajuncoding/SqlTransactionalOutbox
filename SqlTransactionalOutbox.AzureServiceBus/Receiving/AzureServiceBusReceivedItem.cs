using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using SqlTransactionalOutbox.Receiving;

namespace SqlTransactionalOutbox.AzureServiceBus.Receiving
{
    public class AzureServiceBusReceivedItem<TUniqueIdentifier, TPayload> : OutboxReceivedItem<TUniqueIdentifier, TPayload>
    {
        public Message AzureServiceBusMessage { get; }

        public AzureServiceBusReceivedItem(
            Message azureServiceBusMessage,
            ISqlTransactionalOutboxItem<TUniqueIdentifier> outboxItem, 
            ILookup<string, object> headersLookup, 
            Func<Task> acknowledgeReceiptAsyncFunc, 
            Func<Task> rejectAbandonReceiptAsyncFunc, 
            Func<Task> rejectDeadLetterReceiptAsyncFunc, 
            Func<ISqlTransactionalOutboxItem<TUniqueIdentifier>, TPayload> parsePayloadFunc, 
            bool enableFifoEnforcedReceiving
        )
        : base(
            outboxItem, 
            headersLookup, 
            azureServiceBusMessage.ContentType,
            acknowledgeReceiptAsyncFunc, 
            rejectAbandonReceiptAsyncFunc, 
            rejectDeadLetterReceiptAsyncFunc, 
            parsePayloadFunc, 
            enableFifoEnforcedReceiving,
            fifoGroupingIdentifier: azureServiceBusMessage.SessionId,
            correlationId: azureServiceBusMessage.CorrelationId
        )
        {
            //Provide access to the original Azure Service Bus Message for customized Advanced functionality.
            this.AzureServiceBusMessage = azureServiceBusMessage;
        }
    }
}
