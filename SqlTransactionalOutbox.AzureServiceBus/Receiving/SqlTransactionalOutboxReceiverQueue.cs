using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class SqlTransactionalOutboxReceiverQueue<TUniqueIdentifier, TPayload>
    {
         protected Channel<ISqlTransactionalOutboxReceivedItem<TUniqueIdentifier, TPayload>> ChannelQueue { get; }
             = Channel.CreateUnbounded<ISqlTransactionalOutboxReceivedItem<TUniqueIdentifier, TPayload>>();

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
    }
}
