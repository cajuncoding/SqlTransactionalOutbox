using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutboxHelpers
{
    public class DefaultOutboxTableFields : ISqlTransactionalOutboxTableFields
    {
        public string UniqueIdentifierFieldName { get; } = nameof(ISqlTransactionalOutboxItem.UniqueIdentifier);
        public string StatusFieldName { get; } = nameof(ISqlTransactionalOutboxItem.Status);
        public string PublishingTargetFieldName { get; } = nameof(ISqlTransactionalOutboxItem.PublishingTarget);
        public string PublishingPayloadFieldName { get; } = nameof(ISqlTransactionalOutboxItem.PublishingPayload);
        public string PublishingAttemptsFieldName { get; } = nameof(ISqlTransactionalOutboxItem.PublishingAttempts);
        public string CreatedDateTimeUtcFieldName { get; } = nameof(ISqlTransactionalOutboxItem.CreatedDateTimeUtc);
    }
}
