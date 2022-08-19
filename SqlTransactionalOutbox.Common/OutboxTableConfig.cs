using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutbox
{
    public class OutboxTableConfig : ISqlTransactionalOutboxTableConfig
    {
        /// <summary>
        /// Configuration for the Outbox Database Table; any values not specified or set to null will use Default values.
        /// </summary>
        /// <param name="transactionalOutboxSchemaName"></param>
        /// <param name="transactionalOutboxTableName"></param>
        /// <param name="pkeyFieldName"></param>
        /// <param name="uniqueIdentifierFieldName"></param>
        /// <param name="fifoGroupingIdentifier"></param>
        /// <param name="statusFieldName"></param>
        /// <param name="publishTargetFieldName"></param>
        /// <param name="payloadFieldName"></param>
        /// <param name="publishAttemptsFieldName"></param>
        /// <param name="createdDateTimeUtcFieldName"></param>
        public OutboxTableConfig(
            string transactionalOutboxSchemaName = null,
            string transactionalOutboxTableName = null,
            string pkeyFieldName = null,
            string uniqueIdentifierFieldName = null,
            string fifoGroupingIdentifier = null,
            string statusFieldName = null,
            string publishTargetFieldName = null,
            string payloadFieldName = null,
            string publishAttemptsFieldName = null,
            string createdDateTimeUtcFieldName = null
        )
        {
            TransactionalOutboxSchemaName = transactionalOutboxSchemaName ?? DefaultTransactionalOutboxSchemaName;
            TransactionalOutboxTableName = transactionalOutboxTableName ?? DefaultTransactionalOutboxTableName;

            PKeyFieldName = pkeyFieldName ?? DefaultPKeyFieldName;
            UniqueIdentifierFieldName = uniqueIdentifierFieldName ?? nameof(OutboxProcessingItem<Guid>.UniqueIdentifier);
            FifoGroupingIdentifier = fifoGroupingIdentifier ?? nameof(OutboxProcessingItem<Guid>.FifoGroupingIdentifier);
            StatusFieldName = statusFieldName ?? nameof(OutboxProcessingItem<Guid>.Status);
            PublishTargetFieldName = publishTargetFieldName ?? nameof(OutboxProcessingItem<Guid>.PublishTarget);
            PayloadFieldName = payloadFieldName ?? nameof(OutboxProcessingItem<Guid>.Payload);
            PublishAttemptsFieldName = publishAttemptsFieldName ?? nameof(OutboxProcessingItem<Guid>.PublishAttempts);
            CreatedDateTimeUtcFieldName = createdDateTimeUtcFieldName ?? nameof(OutboxProcessingItem<Guid>.CreatedDateTimeUtc);
        }

        public const string DefaultTransactionalOutboxSchemaName = "notifications";
        public const string DefaultTransactionalOutboxTableName = "TransactionalOutboxQueue";
        public const string DefaultPKeyFieldName = "Id";

        public string TransactionalOutboxSchemaName { get; protected set; }
        public string TransactionalOutboxTableName { get; protected set; }

        //NOTE: The PKey Field is only used for Sql Server specific resolution of DateTime sort collisions & Sorting,
        //  but is otherwise not needed for Outbox Item Model.
        public string PKeyFieldName { get; protected set; }
        public string UniqueIdentifierFieldName { get; protected set; }
        public string FifoGroupingIdentifier { get; protected set; }
        public string StatusFieldName { get; protected set; }
        public string PublishTargetFieldName { get; protected set; }
        public string PayloadFieldName { get; protected set; }
        public string PublishAttemptsFieldName { get; protected set; }
        public string CreatedDateTimeUtcFieldName { get; protected set; }
    }
}
