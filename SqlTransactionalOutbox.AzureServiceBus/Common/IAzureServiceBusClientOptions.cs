using System;
using System.Collections.Generic;
using System.Text;
using Azure.Messaging.ServiceBus;

namespace SqlTransactionalOutbox.AzureServiceBus.Common
{
    public interface IAzureServiceBusClientOptions
    {
        ServiceBusClientOptions ServiceBusClientOptions { get; set; }

        /// <summary>
        /// An hook/callback for handling informational logging.
        /// </summary>
        Action<string> LogDebugCallback { get; set; }

        /// <summary>
        /// A hook/callback for handling error/exception logging.
        /// </summary>
        Action<Exception> LogErrorCallback { get; set; }
    }
}
