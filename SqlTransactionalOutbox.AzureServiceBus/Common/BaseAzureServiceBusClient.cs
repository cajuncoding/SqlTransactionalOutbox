using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using SqlTransactionalOutbox.AzureServiceBus.Receiving;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox.AzureServiceBus.Common
{
    public abstract class BaseAzureServiceBusClient: IAsyncDisposable
    {
        protected Action<ServiceBusProcessor> ProcessingClientConfigurationFunc { get; set; } = null;
        protected Action<ServiceBusSessionProcessor> SessionProcessingClientConfigurationFunc { get; set; } = null;

        protected ServiceBusClient AzureServiceBusClient { get; set; }
        protected bool DisposingEnabled { get; set; } = false;

        /// <summary>
        /// Provide configuration hook for customization of the Azure Service Bus clients that are initialized;
        /// this is useful for registering plugins and/or other customizations of the Clients used.
        /// </summary>
        /// <param name="clientConfigurationFunc"></param>
        public virtual void ConfigureClientWith(Action<ServiceBusProcessor> clientConfigurationFunc)
        {
            ProcessingClientConfigurationFunc = clientConfigurationFunc.AssertNotNull(nameof(clientConfigurationFunc));
        }

        /// <summary>
        /// Provide configuration hook for customization of the Azure Service Bus clients that are initialized;
        /// this is useful for registering plugins and/or other customizations of the Clients used.
        /// </summary>
        /// <param name="clientConfigurationFunc"></param>
        public virtual void ConfigureClientWith(Action<ServiceBusSessionProcessor> clientConfigurationFunc)
        {
            SessionProcessingClientConfigurationFunc = clientConfigurationFunc.AssertNotNull(nameof(clientConfigurationFunc));
        }

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
