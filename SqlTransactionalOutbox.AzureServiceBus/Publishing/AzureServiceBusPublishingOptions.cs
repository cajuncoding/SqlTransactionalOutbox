using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Microsoft.Azure.ServiceBus;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class AzureServiceBusPublishingOptions
    {
        /// <summary>
        /// The Retry Policy to be used when publishing to Azure Event Bus
        /// </summary>
        public RetryPolicy RetryPolicy { get; set; } = RetryPolicy.Default;

        /// <summary>
        /// Configure if there should be validation when attempting to parse the Payload
        /// as Json for dynamically defined Message parameters.  If true, then exceptions
        /// will be thrown if Json processing fails, otherwise it will be treated as a string
        /// with no additional dynamic items populated from the Message structure; and the entire
        /// payload will be sent.
        /// </summary>
        public bool ThrowExceptionOnJsonPayloadParseFailure { get; set; } = false;

        /// <summary>
        /// Default prefix for the Label if not dynamically defined by Json Payload; this prefix
        /// is prepended to the Label to provide a systematic Label that helps denote what system
        /// published the message.
        /// </summary>
        public string SenderApplicationName { get; set; } 
            = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Name;

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
