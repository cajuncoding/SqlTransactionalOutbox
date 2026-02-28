using System;

namespace SqlTransactionalOutbox
{
    public interface ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload>
    {
        ISqlTransactionalOutboxItem<TUniqueIdentifier> CreateNewOutboxItem(
            string publishingTarget,
            TPayload publishingPayload,
            string fifoGroupingIdentifier = null,
            DateTimeOffset? scheduledPublishDateTimeUtc = null
        );
        
        ISqlTransactionalOutboxItem<TUniqueIdentifier> CreateExistingOutboxItem(
            string uniqueIdentifier,
            DateTimeOffset createdDateTimeUtc,
            DateTimeOffset? scheduledPublishDateTimeUtc,
            string status,
            string fifoGroupingIdentifier,
            int publishAttempts,
            string publishTarget, 
            //NOTE: When Creating an Existing Item we always take in the Serialized Payload
            string serializedPayload
        );

        ISqlTransactionalOutboxItem<TUniqueIdentifier> CreateExistingOutboxItem(
            TUniqueIdentifier uniqueIdentifier,
            DateTimeOffset createdDateTimeUtc,
            DateTimeOffset? scheduledPublishDateTimeUtc,
            string status,
            string fifoGroupingIdentifier,
            int publishAttempts,
            string publishTarget,
            //NOTE: When Creating an Existing Item we always take in the Serialized Payload
            string serializedPayload
        );

        TPayload ParsePayload(ISqlTransactionalOutboxItem<TUniqueIdentifier> outboxItem);
    }
}
