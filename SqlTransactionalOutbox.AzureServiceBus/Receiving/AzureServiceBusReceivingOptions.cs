using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;
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
