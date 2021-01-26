using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutbox
{
    public interface ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload>
    {
        ISqlTransactionalOutboxItem<TUniqueIdentifier> CreateNewOutboxItem(
            string publishingTarget,
            TPayload publishingPayload
        );
        
        ISqlTransactionalOutboxItem<TUniqueIdentifier> CreateExistingOutboxItem(
            string uniqueIdentifier,
            DateTime createdDateTimeUtc,
            string status,
            int publishAttempts,
            string publishTarget, 
            //NOTE: When Creating an Existing Item we always take in the Serialized Payload
            string serializedPayload
        );

        ISqlTransactionalOutboxItem<TUniqueIdentifier> CreateExistingOutboxItem(
            TUniqueIdentifier uniqueIdentifier,
            DateTime createdDateTimeUtc,
            string status,
            int publishAttempts,
            string publishTarget,
            //NOTE: When Creating an Existing Item we always take in the Serialized Payload
            string serializedPayload
        );

        TPayload ParsePayload(ISqlTransactionalOutboxItem<TUniqueIdentifier> outboxItem);
    }
}
