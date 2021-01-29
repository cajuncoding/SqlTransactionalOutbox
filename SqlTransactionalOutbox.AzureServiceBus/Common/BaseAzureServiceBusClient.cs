using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.ServiceBus;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox.AzureServiceBus.Common
{
    public abstract class BaseAzureServiceBusClient
    {
        protected Action<IClientEntity> ClientConfigurationFunc = null;

        /// <summary>
        /// Provide configuration hook for customization of the Azure Service Bus clients that are initialized;
        /// this is useful for registering plugins and/or other customizations of the Clients used.
        /// </summary>
        /// <param name="clientConfigurationFunc"></param>
        public virtual void ConfigureClientWith(Action<IClientEntity> clientConfigurationFunc)
        {
            ClientConfigurationFunc = clientConfigurationFunc.AssertNotNull(nameof(clientConfigurationFunc));
        }

    }
}
