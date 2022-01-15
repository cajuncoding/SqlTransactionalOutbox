using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using SqlTransactionalOutbox.AzureServiceBus.Receiving;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox.AzureServiceBus.Common
{
    public abstract class BaseAzureServiceBusClient: IAsyncDisposable
    {
        protected ServiceBusClient AzureServiceBusClient { get; set; }
        protected bool DisposingEnabled { get; set; } = false;

        public string ServiceBusTopic { get; set; }

        public virtual async ValueTask DisposeAsync()
        {
            if (DisposingEnabled)
            {
                if (this.AzureServiceBusClient != null)
                    await this.AzureServiceBusClient.DisposeAsync();
            }
        }

        protected static ServiceBusClient InitAzureServiceBusConnection(string azureServiceBusConnectionString, IAzureServiceBusClientOptions options)
        {
            azureServiceBusConnectionString.AssertNotNullOrWhiteSpace(nameof(azureServiceBusConnectionString));

            var serviceBusClient = new ServiceBusClient(
                azureServiceBusConnectionString,
                options?.ServiceBusClientOptions ?? new ServiceBusClientOptions()
            );

            return serviceBusClient;
        }
    }
}
