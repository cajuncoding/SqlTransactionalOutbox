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
            string serviceBusTopic,
            string serviceBusSubscription,
            ISqlTransactionalOutboxItemFactory<Guid, TPayload> outboxItemFactory = null,
            AzureServiceBusReceivingOptions options = null
        ) 
        : base(
            azureServiceBusConnectionString,
            serviceBusTopic,
            serviceBusSubscription,
            outboxItemFactory ?? new DefaultOutboxItemFactory<TPayload>(),
            InitOptions(options ?? new AzureServiceBusReceivingOptions())
        )
        { }

        private static AzureServiceBusReceivingOptions InitOptions(AzureServiceBusReceivingOptions options)
        {
            //FORCE FIFO processing to be enabled!
            options.FifoEnforcedReceivingEnabled = true;
            return options;
        }
    }
}
