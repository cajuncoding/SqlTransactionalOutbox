using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutboxHelpers
{
    public class DefaultOutboxTableFields : ISqlTransactionalOutboxTableFields
    {
        public string UUIDFieldName { get; } = nameof(OutboxItem.UUID);
        public string StatusFieldName { get; } = nameof(OutboxItem.Status);
        public string PublishingTargetFieldName { get; } = nameof(OutboxItem.PublishingTarget);
        public string PublishingPayloadFieldName { get; } = nameof(OutboxItem.PublishingPayload);
        public string PublishingAttemptsFieldName { get; } = nameof(OutboxItem.PublishingAttempts);
        public string CreatedDateTimeUtcFieldName { get; } = nameof(OutboxItem.CreatedDateTimeUtc);
        public string ExpirationDateTimeUtcFieldName { get; } = nameof(OutboxItem.ExpirationDateTimeUtc);
    }
}
