using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutbox
{
    public class OutboxTableConfig : ISqlTransactionalOutboxTableConfig
    {
        public const string DefaultTransactionalOutboxSchemaName = "notifications";
        public const string DefaultTransactionalOutboxTableName = "TransactionalOutboxQueue";
        public const string DefaultPKeyFieldName = "Id";

        public string TransactionalOutboxSchemaName { get; } = DefaultTransactionalOutboxSchemaName;
        public string TransactionalOutboxTableName { get; } = DefaultTransactionalOutboxTableName;

        //NOTE: The PKey Field is only used for Sql Server specific resolution of DateTime sort collisions & Sorting,
        //  but is otherwise not needed for Outbox Item Model.
        public string PKeyFieldName { get; } = DefaultPKeyFieldName;
        public string UniqueIdentifierFieldName { get; } = nameof(OutboxProcessingItem<Guid>.UniqueIdentifier);
        public string StatusFieldName { get; } = nameof(OutboxProcessingItem<Guid>.Status);
        public string PublishingTargetFieldName { get; } = nameof(OutboxProcessingItem<Guid>.PublishingTarget);
        public string PublishingPayloadFieldName { get; } = nameof(OutboxProcessingItem<Guid>.PublishingPayload);
        public string PublishingAttemptsFieldName { get; } = nameof(OutboxProcessingItem<Guid>.PublishingAttempts);
        public string CreatedDateTimeUtcFieldName { get; } = nameof(OutboxProcessingItem<Guid>.CreatedDateTimeUtc);
    }
}
