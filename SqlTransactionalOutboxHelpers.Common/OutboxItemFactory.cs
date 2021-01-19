#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
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
            //Validate key required values that are always user provided in this one place...
            publishingTarget.AssertNotNullOrWhiteSpace(nameof(publishingTarget));
            publishingPayload.AssertNotNull(nameof(publishingPayload));

            //Serialize the Payload for Storage with our Outbox Item...
            var serializedPayload = PayloadSerializer.SerializePayload(publishingPayload);

            //Now we can create the fully validated Outbox Item
            var outboxItem = new OutboxItem()
            {
                //Initialize Internal Variables
                UniqueIdentifier = UniqueIdentifierFactory.CreateUniqueIdentifier(),
                Status = OutboxItemStatus.Pending,
                PublishingAttempts = 0,
                CreatedDateTimeUtc = DateTime.UtcNow,
                //Initialize client provided details
                PublishingTarget = publishingTarget,
                PublishingPayload = serializedPayload
            };

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
                AssertInvalidArgument(nameof(uniqueIdentifier), uniqueIdentifier.ToString());

            if(createdDateTimeUtc == DateTime.MinValue)
                AssertInvalidArgument(nameof(createdDateTimeUtc), createdDateTimeUtc.ToString(CultureInfo.InvariantCulture));

            if (publishingAttempts < 0)
                AssertInvalidArgument(nameof(publishingAttempts), publishingAttempts.ToString());


            //Validate key required values that are always user provided in this one place...
            publishingTarget.AssertNotNullOrWhiteSpace(nameof(publishingTarget));
            serializedPayload.AssertNotNullOrWhiteSpace(nameof(serializedPayload));

            //Now we can create the fully validated Outbox Item
            var outboxItem = new OutboxItem()
            {
                //Initialize Internal Variables
                UniqueIdentifier = uniqueIdentifier,
                Status = Enum.Parse<OutboxItemStatus>(status),
                PublishingAttempts = publishingAttempts,
                CreatedDateTimeUtc = createdDateTimeUtc,
                //Initialize client provided details
                PublishingTarget = publishingTarget,
                PublishingPayload = serializedPayload
            };

            return outboxItem;
        }

        private void AssertInvalidArgument(string argName, string value)
        {
            throw new ArgumentException(argName, $"Invalid value of [{value}] specified.");
        }
    }
}
