using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SqlTransactionalOutbox.AzureServiceBus.Receiving
{
    public class SqlTransactionalOutboxReceiverQueue<TUniqueIdentifier, TPayload> : IAsyncDisposable
    {
        protected Channel<ISqlTransactionalOutboxReceivedItem<TUniqueIdentifier, TPayload>> ChannelQueue { get; }
             = Channel.CreateUnbounded<ISqlTransactionalOutboxReceivedItem<TUniqueIdentifier, TPayload>>();

        protected Func<Task> DisposedCallbackAsyncHandler = null;

        public SqlTransactionalOutboxReceiverQueue(Func<Task> disposedCallbackAsyncHandler = null)
        {
            DisposedCallbackAsyncHandler = disposedCallbackAsyncHandler;
        }

        public virtual async Task AddAsync(
            ISqlTransactionalOutboxReceivedItem<TUniqueIdentifier, TPayload> item,
            CancellationToken cancellationToken = default
        )
        {
            await ChannelQueue.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
        }

        public virtual async Task<ISqlTransactionalOutboxReceivedItem<TUniqueIdentifier, TPayload>> TakeAsync(
            CancellationToken cancellationToken = default
        )
        {
            //TODO: Implement CancellationToken wrapper with Timeout To Cancel!
            var item = await ChannelQueue.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            return item;
        }

        public virtual async ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken)
        {
            return await ChannelQueue.Reader.WaitToReadAsync(cancellationToken);
        }

        public virtual async IAsyncEnumerable<ISqlTransactionalOutboxReceivedItem<TUniqueIdentifier, TPayload>> AsEnumerableAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default
        )
        {
            await foreach (var item in ChannelQueue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return item;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (DisposedCallbackAsyncHandler != null)
                await DisposedCallbackAsyncHandler.Invoke().ConfigureAwait(false);
        }
    }
}
