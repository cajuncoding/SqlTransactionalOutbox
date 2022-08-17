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

        public virtual string TransactionalOutboxSchemaName { get; } = DefaultTransactionalOutboxSchemaName;
        public virtual string TransactionalOutboxTableName { get; } = DefaultTransactionalOutboxTableName;

        //NOTE: The PKey Field is only used for Sql Server specific resolution of DateTime sort collisions & Sorting,
        //  but is otherwise not needed for Outbox Item Model.
        public virtual string PKeyFieldName { get; } = DefaultPKeyFieldName;
        public virtual string UniqueIdentifierFieldName { get; } = nameof(OutboxProcessingItem<Guid>.UniqueIdentifier);
        public virtual string FifoGroupingIdentifier { get; } = nameof(OutboxProcessingItem<Guid>.FifoGroupingIdentifier);
        public virtual string StatusFieldName { get; } = nameof(OutboxProcessingItem<Guid>.Status);
        public virtual string PublishTargetFieldName { get; } = nameof(OutboxProcessingItem<Guid>.PublishTarget);
        public virtual string PayloadFieldName { get; } = nameof(OutboxProcessingItem<Guid>.Payload);
        public virtual string PublishAttemptsFieldName { get; } = nameof(OutboxProcessingItem<Guid>.PublishAttempts);
        public virtual string CreatedDateTimeUtcFieldName { get; } = nameof(OutboxProcessingItem<Guid>.CreatedDateTimeUtc);
    }
}
