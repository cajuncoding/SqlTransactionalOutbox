using System;

namespace SqlTransactionalOutbox
{
    public class OutboxInsertionItem<TPayload> : ISqlTransactionalOutboxInsertionItem<TPayload>
    {
        public OutboxInsertionItem(
            string publishingTarget,
            TPayload publishingPayload,
            string fifoGroupingIdentifier = null,
            DateTimeOffset? scheduledPublishDateTimeUtc = null
        ) {
            PublishingTarget = publishingTarget;
            PublishingPayload = publishingPayload;
            FifoGroupingIdentifier = fifoGroupingIdentifier;
            ScheduledPublishDateTimeUtc = scheduledPublishDateTimeUtc;
        }

        public string PublishingTarget { get; set; }
        public TPayload PublishingPayload { get; set; }
        public string FifoGroupingIdentifier { get; set; }
        public DateTimeOffset? ScheduledPublishDateTimeUtc { get; set; }
    }
}
