using System;
using Microsoft.Azure.ServiceBus.Core;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class AzureReceiverClientCache : BaseSqlTransactionalOutboxCache<IReceiverClient>
    {
        public IReceiverClient InitializeReceiverClient(string topicPath, string subscriptionName, Func<IReceiverClient> newReceiverClientFactory)
        {
            var receiverClientCacheKey = $"{topicPath}+{subscriptionName}";
            var receiverClient = this.InitializeItem(receiverClientCacheKey, newReceiverClientFactory);
            return receiverClient;
        }
    }
}
