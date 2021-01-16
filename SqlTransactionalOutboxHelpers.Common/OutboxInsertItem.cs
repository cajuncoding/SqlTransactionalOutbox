using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutboxHelpers
{
    public class OutboxInsertItem<TPayload>
    {
        public OutboxInsertItem(string publishingTarget, TPayload publishingPayload)
        {
            PublishingTarget = publishingTarget;
            PublishingPayload = publishingPayload;
        }

        public string PublishingTarget { get; set; }
        public TPayload PublishingPayload { get; set; }
    }
}
