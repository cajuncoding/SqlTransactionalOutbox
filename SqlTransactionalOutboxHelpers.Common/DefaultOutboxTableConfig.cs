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

        //NOTE: The PKey Field is only used for Sql Server specific resolution of DateTime sort collisions & Sorting,
        //  but is otherwise not needed for Outbox Item Model.
        public string PKeyFieldName { get; } = "Id";
        public string UniqueIdentifierFieldName { get; } = nameof(OutboxItem.UniqueIdentifier);
        public string StatusFieldName { get; } = nameof(OutboxItem.Status);
        public string PublishingTargetFieldName { get; } = nameof(OutboxItem.PublishingTarget);
        public string PublishingPayloadFieldName { get; } = nameof(OutboxItem.PublishingPayload);
        public string PublishingAttemptsFieldName { get; } = nameof(OutboxItem.PublishingAttempts);
        public string CreatedDateTimeUtcFieldName { get; } = nameof(OutboxItem.CreatedDateTimeUtc);
    }
}
