#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using SqlTransactionalOutboxHelpers.CustomExtensions;

namespace SqlTransactionalOutboxHelpers
{
    public class OutboxItemFactory<TPayload> : ISqlTransactionalOutboxItemFactory<Guid, TPayload>
    {
        protected ISqlTransactionalOutboxUniqueIdFactory<Guid> UniqueIdentifierFactory { get; }
        protected ISqlTransactionalOutboxSerializer PayloadSerializer { get; }

        public OutboxItemFactory(
            ISqlTransactionalOutboxUniqueIdFactory<Guid>? uniqueIdFactory = null,
            ISqlTransactionalOutboxSerializer? payloadSerializer = null
        )
        {
            this.UniqueIdentifierFactory = uniqueIdFactory ?? new OutboxItemUniqueIdentifierGuidFactory();
            this.PayloadSerializer = payloadSerializer ?? new JsonOutboxPayloadSerializer();
        }

        public virtual ISqlTransactionalOutboxItem<Guid> CreateNewOutboxItem(
            string publishingTarget,
            TPayload publishingPayload
        )
        {
            var outboxItem = CreateOutboxItemInternal(
                publishingTarget: publishingTarget,  //Will be validated by lower level code.
                publishingPayload: publishingPayload //Will be validated by lower level code.
            );

            return outboxItem;
        }

        public virtual ISqlTransactionalOutboxItem<Guid> CreateExistingOutboxItem(
            Guid uniqueIdentifier,
            string status,
            int publishingAttempts,
            DateTime createdDateTimeUtc,
            string publishingTarget,
            string serializedPayload
        )
        {
            if (uniqueIdentifier == Guid.Empty)
                throw new ArgumentNullException(nameof(uniqueIdentifier));

            //Deserialize (re-hydrate) the TPayload type from the serialized value provided....
            var deserializedPayload = PayloadSerializer.DeserializePayload<TPayload>(serializedPayload);

            var outboxItem = CreateOutboxItemInternal(
                uniqueIdentifier,
                status.AssertNotNullOrWhiteSpace(nameof(status)),
                publishingAttempts,
                createdDateTimeUtc,
                publishingTarget, //Will be validated by lower level code.
                deserializedPayload //Will be validated by lower level code.
            );

            return outboxItem;
        }

        protected virtual ISqlTransactionalOutboxItem<Guid> CreateOutboxItemInternal(
            Guid? uniqueIdentifier = null,
            string? status = null,
            int publishingAttempts = 0,
            DateTime? createdDateTimeUtc = null,
            string? publishingTarget = null,
            TPayload publishingPayload = default
        )
        {
            //Validate key required values that are always user provided in this one place...
            publishingTarget.AssertNotNullOrWhiteSpace(nameof(publishingTarget));
            publishingPayload.AssertNotNull(nameof(publishingPayload));

            //Serialize the Payload for Storage with our Outbox Item...
            var serializedPayload = PayloadSerializer.SerializePayload(publishingPayload);

            //Now we can create the fully validated Outbox Item
            var outboxItem = new OutboxItem()
            {
                //Initialize Internal Variables
                UniqueIdentifier = uniqueIdentifier ?? UniqueIdentifierFactory.CreateUniqueIdentifier(),
                Status = string.IsNullOrWhiteSpace(status) ? OutboxItemStatus.Pending : Enum.Parse<OutboxItemStatus>(status),
                PublishingAttempts = Math.Max(publishingAttempts, 0),
                CreatedDateTimeUtc = createdDateTimeUtc ?? DateTime.UtcNow,
                //Initialize client provided details
                PublishingTarget = publishingTarget,
                PublishingPayload = serializedPayload
            };

            return outboxItem;
        }
    }
}
