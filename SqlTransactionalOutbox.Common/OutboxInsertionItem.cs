using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutbox
{
    public class OutboxInsertionItem<TPayload> : ISqlTransactionalOutboxInsertionItem<TPayload>
    {
        public OutboxInsertionItem(string publishingTarget, TPayload publishingPayload)
        {
            PublishingTarget = publishingTarget;
            PublishingPayload = publishingPayload;
        }

        public string PublishingTarget { get; set; }
        public TPayload PublishingPayload { get; set; }
    }
}
