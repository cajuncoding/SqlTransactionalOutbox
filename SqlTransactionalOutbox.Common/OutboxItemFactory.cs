#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox
{
    public class OutboxItemFactory<TUniqueIdentifier, TPayload> : ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload>
    {
        public ISqlTransactionalOutboxUniqueIdFactory<TUniqueIdentifier> UniqueIdentifierFactory { get; }
        public ISqlTransactionalOutboxSerializer PayloadSerializer { get; }

        public OutboxItemFactory(
            ISqlTransactionalOutboxUniqueIdFactory<TUniqueIdentifier> uniqueIdFactory,
            ISqlTransactionalOutboxSerializer? payloadSerializer = null
        )
        {
            this.UniqueIdentifierFactory = uniqueIdFactory.AssertNotNull(nameof(uniqueIdFactory));
            this.PayloadSerializer = payloadSerializer ?? new OutboxPayloadJsonSerializer();
        }

        public virtual ISqlTransactionalOutboxItem<TUniqueIdentifier> CreateNewOutboxItem(
            string publishingTarget,
            TPayload publishingPayload,
            string? fifoGroupingIdentifier = null
        )
        {
            //Validate key required values that are always user provided in this one place...
            publishingTarget.AssertNotNullOrWhiteSpace(nameof(publishingTarget));
            publishingPayload.AssertNotNull(nameof(publishingPayload));

            //Serialize the Payload for Storage with our Outbox Item...
            var serializedPayload = PayloadSerializer.SerializePayload(publishingPayload);

            //TODO: Implement Logic for Compression Handling!

            //Now we can create the fully validated Outbox Item
            var outboxItem = new OutboxProcessingItem<TUniqueIdentifier>()
            {
                //Initialize Internal Variables
                UniqueIdentifier = UniqueIdentifierFactory.CreateUniqueIdentifier(),
                Status = OutboxItemStatus.Pending,
                FifoGroupingIdentifier = fifoGroupingIdentifier,
                PublishAttempts = 0,
                CreatedDateTimeUtc = DateTime.UtcNow,
                //Initialize client provided details
                PublishTarget = publishingTarget,
                Payload = serializedPayload
            };

            return outboxItem;
        }

        public virtual ISqlTransactionalOutboxItem<TUniqueIdentifier> CreateExistingOutboxItem(
            string uniqueIdentifier,
            DateTime createdDateTimeUtc,
            string status,
            string fifoGroupingIdentifier,
            int publishAttempts,
            string publishTarget,
            string serializedPayload
        )
        {
            uniqueIdentifier.AssertNotNullOrWhiteSpace(nameof(uniqueIdentifier));

            return CreateExistingOutboxItem(
                UniqueIdentifierFactory.ParseUniqueIdentifier(uniqueIdentifier),
                createdDateTimeUtc,
                status,
                fifoGroupingIdentifier,
                publishAttempts,
                publishTarget,
                serializedPayload
            );
        }

        public virtual ISqlTransactionalOutboxItem<TUniqueIdentifier> CreateExistingOutboxItem(
            TUniqueIdentifier uniqueIdentifier,
            DateTime createdDateTimeUtc,
            string status,
            string fifoGroupingIdentifier,
            int publishAttempts,
            string publishTarget,
            string serializedPayload
        )
        {
            uniqueIdentifier.AssertNotNull(nameof(uniqueIdentifier));

            if (createdDateTimeUtc == default)
                AssertInvalidArgument(nameof(createdDateTimeUtc), createdDateTimeUtc.ToString(CultureInfo.InvariantCulture));

            if (publishAttempts < 0)
                AssertInvalidArgument(nameof(publishAttempts), publishAttempts.ToString());


            //Validate key required values that are always user provided in this one place...
            publishTarget.AssertNotNullOrWhiteSpace(nameof(publishTarget));
            serializedPayload.AssertNotNullOrWhiteSpace(nameof(serializedPayload));

            //Now we can create the fully validated Outbox Item
            var outboxItem = new OutboxProcessingItem<TUniqueIdentifier>()
            {
                //Initialize Internal Variables
                UniqueIdentifier = uniqueIdentifier,
                FifoGroupingIdentifier = fifoGroupingIdentifier,
                Status = Enum.Parse<OutboxItemStatus>(status),
                PublishAttempts = publishAttempts,
                CreatedDateTimeUtc = createdDateTimeUtc,
                //Initialize client provided details
                PublishTarget = publishTarget,
                Payload = serializedPayload
            };

            return outboxItem;
        }

        public TPayload ParsePayload(ISqlTransactionalOutboxItem<TUniqueIdentifier> outboxItem)
        {
            //TODO: Implement Logic for Compression Handling!
            var payload = PayloadSerializer.DeserializePayload<TPayload>(outboxItem.Payload);
            return payload;
        }

        protected virtual void AssertInvalidArgument(string argName, string value)
        {
            throw new ArgumentException(argName, $"Invalid value of [{value}] specified.");
        }
    }
}
