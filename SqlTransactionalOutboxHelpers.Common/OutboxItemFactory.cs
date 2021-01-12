#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using SqlTransactionalOutboxHelpers.CustomExtensions;

namespace SqlTransactionalOutboxHelpers
{
    public class OutboxItemFactory : ISqlTransactionalOutboxItemFactory
    {
        public virtual ISqlTransactionalOutboxItem CreateExistingOutboxItem(
            string uniqueIdentifier,
            string status,
            int publishingAttempts,
            DateTime createdDateTimeUtc,
            string publishingTarget,
            string publishingPayload
        )
        {
            var outboxItem = CreateOutboxItemInternal(
                uniqueIdentifier.AssertNotNullOrWhiteSpace(nameof(uniqueIdentifier)),
                status.AssertNotNullOrWhiteSpace(nameof(status)),
                publishingAttempts,
                createdDateTimeUtc,
                publishingTarget, //Will be validated by lower level code.
                publishingPayload //Will be validated by lower level code.
            );
            return outboxItem;
        }


        public virtual ISqlTransactionalOutboxItem CreateNewOutboxItem(
            string publishingTarget, 
            string publishingPayload
        )
        {
            var outboxItem = CreateOutboxItemInternal(
                publishingTarget: publishingTarget,  //Will be validated by lower level code.
                publishingPayload: publishingPayload //Will be validated by lower level code.
            );

            return outboxItem;
        }

        protected virtual ISqlTransactionalOutboxItem CreateOutboxItemInternal(
            string? uniqueIdentifier = null,
            string? status = null,
            int publishingAttempts = 0,
            DateTime? createdDateTimeUtc = null,
            string? publishingTarget = null,
            string? publishingPayload = null
        )
        {
            //Validate key required values that are always user provided in this one place...
            publishingTarget.AssertNotNullOrWhiteSpace(nameof(publishingTarget));
            publishingPayload.AssertNotNullOrWhiteSpace(nameof(publishingPayload));

            var outboxItem = new OutboxItem()
            {
                //Initialize Internal Variables
                UniqueIdentifier = uniqueIdentifier ?? CreateUniqueIdentifier(),
                Status = string.IsNullOrWhiteSpace(status) ? OutboxItemStatus.Pending : Enum.Parse<OutboxItemStatus>(status),
                PublishingAttempts = Math.Max(publishingAttempts, 0),
                CreatedDateTimeUtc = createdDateTimeUtc ?? DateTime.UtcNow,
                //Initialize client provided details
                PublishingTarget = publishingTarget,
                PublishingPayload = publishingPayload
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
