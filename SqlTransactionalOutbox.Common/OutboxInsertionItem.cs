using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutbox
{
    public class OutboxInsertionItem<TPayload> : ISqlTransactionalOutboxInsertionItem<TPayload>
    {
        public OutboxInsertionItem(string publishingTarget, TPayload publishingPayload, string fifoGroupingIdentifier = null)
        {
            PublishingTarget = publishingTarget;
            PublishingPayload = publishingPayload;
            FifoGroupingIdentifier = fifoGroupingIdentifier;
        }

        public string PublishingTarget { get; set; }
        public TPayload PublishingPayload { get; set; }
        public string FifoGroupingIdentifier { get; set; }
    }
}
