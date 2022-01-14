using System;
using Azure.Messaging.ServiceBus;
using SqlTransactionalOutbox.AzureServiceBus.Common;

namespace SqlTransactionalOutbox.AzureServiceBus.Receiving
{
    public class AzureServiceBusReceivingOptions : IAzureServiceBusClientOptions
    {
        /// <summary>
        /// Determines whether FIFO processing is enforced; for Azure Service Bus this means that a Session
        ///     based Processor/Receiver will be used instead of a standard processor!
        /// </summary>
        public bool FifoEnforcedReceivingEnabled { get; set; } = false;

        public ServiceBusClientOptions ServiceBusClientOptions { get; set; } = new ServiceBusClientOptions();

        public int PrefetchCount { get; set; } = 0;

        public TimeSpan? MaxAutoRenewDuration { get; set; }

        public int MaxConcurrentReceiversOrSessions { get; set; } = 1;

        //[Obsolete("Not available with Azure.Messaging.ServiceBus library.")]
        //public TimeSpan? ClientOperationTimeout { get; set; } = null;

        public TimeSpan? SessionConnectionIdleTimeout { get; set; } = null;

        /// <summary>
        /// An hook/callback for handling informational logging.
        /// </summary>
        public Action<string> LogDebugCallback { get; set; } = null;

        /// <summary>
        /// A hook/callback for handling error/exception logging.
        /// </summary>
        public Action<Exception> LogErrorCallback { get; set; } = null;

    }
}
