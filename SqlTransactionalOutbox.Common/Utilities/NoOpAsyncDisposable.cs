using System;
using System.Threading.Tasks;

namespace SqlTransactionalOutbox.Utilities
{
    public class NoOpAsyncDisposable : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            //DO NOTHING;
            return new ValueTask();
        }
    }
}
