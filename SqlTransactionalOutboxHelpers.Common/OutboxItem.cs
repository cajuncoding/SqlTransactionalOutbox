using System;
using System.Xml;

namespace SqlTransactionalOutboxHelpers
{
    public class OutboxItem
    {
        public static TimeSpan DefaultTimespanToExpiration = TimeSpan.FromDays(5);

        public OutboxItem(string publishingTarget, string publishingPayload, TimeSpan? timeToExpiration = null)
        {
            //Initialize User specified variables
            if (string.IsNullOrWhiteSpace(publishingTarget))
                throw new ArgumentNullException(nameof(publishingTarget),
                    "A notification topic is required and cannot be null or empty."
                );

            if (string.IsNullOrWhiteSpace(publishingTarget))
                throw new ArgumentNullException(nameof(publishingPayload),
                    "A notification payload is required and cannot be null or empty."
                );

            PublishingTarget = publishingTarget;
            PublishingPayload = publishingPayload;

            //Initialize Internal Variables
            UUID = Guid.NewGuid().ToString("B");
            Status = OutboxItemStatus.Pending;
            PublishingAttempts = 0;
            CreatedDateTimeUtc = DateTime.UtcNow;
            ExpirationDateTimeUtc = CreatedDateTimeUtc.Add(timeToExpiration ?? DefaultTimespanToExpiration);
        }
        
        public string UUID { get; }

        public OutboxItemStatus Status { get; set; }

        public string PublishingTarget { get; set; }

        public string PublishingPayload { get; set; }

        public int PublishingAttempts { get; set; }

        public DateTime CreatedDateTimeUtc { get; set; }

        public DateTime ExpirationDateTimeUtc { get; set; }

    }
}
