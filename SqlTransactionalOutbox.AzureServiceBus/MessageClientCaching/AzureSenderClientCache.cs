using System;
using Microsoft.Azure.ServiceBus.Core;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class AzureSenderClientCache : BaseSqlTransactionalOutboxCache<ISenderClient>
    {
        public ISenderClient InitializeClient(string publishingTarget, Func<ISenderClient> newSenderClientFactory)
        {
            var senderClient = this.InitializeItem(publishingTarget, newSenderClientFactory);
            return senderClient;
        }
    }
}
