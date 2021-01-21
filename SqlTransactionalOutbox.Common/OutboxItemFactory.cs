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
        protected ISqlTransactionalOutboxUniqueIdFactory<TUniqueIdentifier> UniqueIdentifierFactory { get; }
        protected ISqlTransactionalOutboxSerializer PayloadSerializer { get; }

        public OutboxItemFactory(
            ISqlTransactionalOutboxUniqueIdFactory<TUniqueIdentifier>? uniqueIdFactory,
            ISqlTransactionalOutboxSerializer? payloadSerializer
        )
        {
            #pragma warning disable 8601
            this.UniqueIdentifierFactory = uniqueIdFactory.AssertNotNull(nameof(uniqueIdFactory));
            this.PayloadSerializer = payloadSerializer.AssertNotNull(nameof(payloadSerializer));
            #pragma warning restore 8601
        }

        public virtual ISqlTransactionalOutboxItem<TUniqueIdentifier> CreateNewOutboxItem(
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
            var outboxItem = new OutboxProcessingItem<TUniqueIdentifier>()
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

        public virtual ISqlTransactionalOutboxItem<TUniqueIdentifier> CreateExistingOutboxItem(
            TUniqueIdentifier uniqueIdentifier,
            string status,
            int publishingAttempts,
            DateTime createdDateTimeUtc,
            string publishingTarget,
            string serializedPayload
        )
        {
            if (uniqueIdentifier == null)
                AssertInvalidArgument(nameof(uniqueIdentifier), uniqueIdentifier?.ToString() ?? "null");

            if(createdDateTimeUtc == default)
                AssertInvalidArgument(nameof(createdDateTimeUtc), createdDateTimeUtc.ToString(CultureInfo.InvariantCulture));

            if (publishingAttempts < 0)
                AssertInvalidArgument(nameof(publishingAttempts), publishingAttempts.ToString());


            //Validate key required values that are always user provided in this one place...
            publishingTarget.AssertNotNullOrWhiteSpace(nameof(publishingTarget));
            serializedPayload.AssertNotNullOrWhiteSpace(nameof(serializedPayload));

            //Now we can create the fully validated Outbox Item
            var outboxItem = new OutboxProcessingItem<TUniqueIdentifier>()
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

        protected virtual void AssertInvalidArgument(string argName, string value)
        {
            throw new ArgumentException(argName, $"Invalid value of [{value}] specified.");
        }
    }
}
