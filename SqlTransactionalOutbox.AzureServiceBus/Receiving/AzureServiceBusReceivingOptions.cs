using System;
using Microsoft.Azure.ServiceBus;

namespace SqlTransactionalOutbox.AzureServiceBus.Receiving
{
    public class AzureServiceBusReceivingOptions
    {
        /// <summary>
        /// 
        /// </summary>
        public bool FifoEnforcedReceivingEnabled { get; set; } = false;

        public TimeSpan MaxAutoRenewDuration { get; set; } = TimeSpan.FromMinutes(5);

        public RetryPolicy RetryPolicy { get; set; } = RetryPolicy.Default;

        public int PrefetchCount { get; set; } = 0;

        public int MaxConcurrentReceiversOrSessions { get; set; } = 1;

        public TimeSpan? ClientOperationTimeout { get; set; } = null;

        public TimeSpan? ConnectionIdleTimeout { get; set; } = null;

        public bool ReleaseSessionWhenNoHandlerIsProvided { get; set; } = true;

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
