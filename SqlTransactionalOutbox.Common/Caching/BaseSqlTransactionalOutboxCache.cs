using System;
using System.Collections.Concurrent;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public abstract class BaseSqlTransactionalOutboxCache<T>
    {
        protected static ConcurrentDictionary<string, Lazy<T>> GenericsItemCache { get; } = new ConcurrentDictionary<string, Lazy<T>>();

        protected virtual T InitializeItem(string clientCacheKey, Func<T> newItemFactory)
        {
            newItemFactory.AssertNotNull(nameof(newItemFactory));

            var lazy = GenericsItemCache.GetOrAdd(
                clientCacheKey.AssertNotNullOrWhiteSpace(nameof(clientCacheKey)), 
                new Lazy<T>(newItemFactory)
            );

            var item = lazy.Value;
            return item;
        }
    }
}
