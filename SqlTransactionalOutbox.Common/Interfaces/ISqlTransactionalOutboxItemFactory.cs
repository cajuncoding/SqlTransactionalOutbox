using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutbox
{
    public interface ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, in TPayload>
    {
        ISqlTransactionalOutboxItem<TUniqueIdentifier> CreateNewOutboxItem(
            string publishingTarget,
            TPayload publishingPayload
        );
        
        ISqlTransactionalOutboxItem<TUniqueIdentifier> CreateExistingOutboxItem(
            TUniqueIdentifier uniqueIdentifier,
            DateTime createdDateTimeUtc,
            string status,
            int publishingAttempts,
            string publishingTarget, 
            //NOTE: When Creating an Existing Item we always take in the Serialized Payload
            string serializedPayload
        );
    }
}
