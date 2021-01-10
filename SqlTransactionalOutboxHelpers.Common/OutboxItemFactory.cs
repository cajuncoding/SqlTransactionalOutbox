using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutboxHelpers
{
    public class OutboxItemFactory : ISqlTransactionalOutboxItemFactory
    {
        public virtual ISqlTransactionalOutboxItem CreateOutboxItem(
            string publishingTarget, 
            string publishingPayload, 
            TimeSpan? timeSpanToLive = null
        )
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

            var outboxItem = new OutboxItem()
            {
                PublishingTarget = publishingTarget,
                PublishingPayload = publishingPayload,
                //Initialize Internal Variables
                UniqueIdentifier = CreateUniqueIdentifier(),
                Status = OutboxItemStatus.Pending,
                PublishingAttempts = 0,
                CreatedDateTimeUtc = DateTime.UtcNow
            };

            return outboxItem;
        }

        /// <summary>
        /// Generate a Unique Identifier using C# GUID; may override this to create ID using different
        /// algorithm if needed.
        /// </summary>
        /// <returns></returns>
        public virtual string CreateUniqueIdentifier()
        {
            return Guid.NewGuid().ToString("B");
        }
    }
}
