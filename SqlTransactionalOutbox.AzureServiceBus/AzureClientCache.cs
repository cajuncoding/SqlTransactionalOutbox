using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.ServiceBus.Core;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class AzureSenderClientCache
    {
        protected static ConcurrentDictionary<string, Lazy<ISenderClient>> SenderClients { get; } = new ConcurrentDictionary<string, Lazy<ISenderClient>>();

        protected Func<string, ISenderClient> SenderClientFactory { get; set; }

        public AzureSenderClientCache(Func<string, ISenderClient> senderClientFactory)
        {
            SenderClientFactory = senderClientFactory.AssertNotNull(nameof(senderClientFactory));
        }

        public virtual ISenderClient InitializeSenderClient(string publishingTarget)
        {
            var senderClientLazy = SenderClients.GetOrAdd(
                publishingTarget, 
                new Lazy<ISenderClient>(() => SenderClientFactory.Invoke(publishingTarget))
            );

            var senderClient = senderClientLazy.Value;
            return senderClient;
        }
    }
}
