using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutboxHelpers
{
    public interface ISqlTransactionalOutboxItemFactory
    {
        ISqlTransactionalOutboxItem CreateExistingOutboxItem(
            string uniqueIdentifier,
            string status,
            int publishingAttempts,
            DateTime createdDateTimeUtc,
            string publishingTarget, 
            string publishingPayload
        );

        ISqlTransactionalOutboxItem CreateNewOutboxItem(
            string publishingTarget, 
            string publishingPayload
        );

        string CreateUniqueIdentifier();
    }
}
