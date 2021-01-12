using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutboxHelpers
{
    public class DefaultOutboxTableConfig : ISqlTransactionalOutboxTableConfig
    {
        public const string DefaultTransactionalOutboxSchemaName = "notifications";
        public const string DefaultTransactionalOutboxTableName = "TransactionalOutboxQueue";

        public string TransactionalOutboxSchemaName { get; } = DefaultTransactionalOutboxSchemaName;
        public string TransactionalOutboxTableName { get; } = DefaultTransactionalOutboxTableName;
        public string UniqueIdentifierFieldName { get; } = nameof(ISqlTransactionalOutboxItem.UniqueIdentifier);
        public string StatusFieldName { get; } = nameof(ISqlTransactionalOutboxItem.Status);
        public string PublishingTargetFieldName { get; } = nameof(ISqlTransactionalOutboxItem.PublishingTarget);
        public string PublishingPayloadFieldName { get; } = nameof(ISqlTransactionalOutboxItem.PublishingPayload);
        public string PublishingAttemptsFieldName { get; } = nameof(ISqlTransactionalOutboxItem.PublishingAttempts);
        public string CreatedDateTimeUtcFieldName { get; } = nameof(ISqlTransactionalOutboxItem.CreatedDateTimeUtc);
    }
}
