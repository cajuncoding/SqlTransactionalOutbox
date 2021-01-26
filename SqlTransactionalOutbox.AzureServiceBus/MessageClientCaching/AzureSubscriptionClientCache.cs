using System;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class AzureSubscriptionClientCache : BaseSqlTransactionalOutboxCache<ISubscriptionClient>
    {
        public ISubscriptionClient InitializeClient(string topicPath, string subscriptionName, Func<ISubscriptionClient> newClientFactory)
        {
            var clientCacheKey = $"{topicPath}::{subscriptionName}";
            var subscriptionClient = this.InitializeItem(clientCacheKey, newClientFactory);
            return subscriptionClient;
        }
    }
}
