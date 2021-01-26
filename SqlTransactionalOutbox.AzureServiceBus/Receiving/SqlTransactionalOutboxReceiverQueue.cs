using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using SqlTransactionalOutbox.Interfaces;

namespace SqlTransactionalOutbox.AzureServiceBus
{
    public class SqlTransactionalOutboxReceiverQueue<TUniqueIdentifier, TPayload>
    {
         protected Channel<ISqlTransactionalOutboxPublishedItem<TUniqueIdentifier, TPayload>> ChannelQueue { get; }
             = Channel.CreateUnbounded<ISqlTransactionalOutboxPublishedItem<TUniqueIdentifier, TPayload>>();

        public virtual async Task AddAsync(
            ISqlTransactionalOutboxPublishedItem<TUniqueIdentifier, TPayload> item,
            CancellationToken cancellationToken = default
        )
        {
            await ChannelQueue.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
        }

        public virtual async Task<ISqlTransactionalOutboxPublishedItem<TUniqueIdentifier, TPayload>> TakeAsync(
            CancellationToken cancellationToken = default
        )
        {
            //TODO: Implement CancellationToken wrapper with Timeout To Cancel!
            var item = await ChannelQueue.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            return item;
        }

        public virtual async IAsyncEnumerable<ISqlTransactionalOutboxPublishedItem<TUniqueIdentifier, TPayload>> AsEnumerableAsync(
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
