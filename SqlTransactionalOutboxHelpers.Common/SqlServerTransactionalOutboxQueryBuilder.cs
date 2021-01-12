using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutboxHelpers
{
    public class SqlServerTransactionalOutboxQueryBuilder
    {
        public ISqlTransactionalOutboxTableConfig OutboxTableConfig { get; protected set; }

        public SqlServerTransactionalOutboxQueryBuilder(ISqlTransactionalOutboxTableConfig outboxTableConfig)
        {
            this.OutboxTableConfig = outboxTableConfig ?? new DefaultOutboxTableConfig();
        }

        public virtual string BuildSqlForRetrieveOutboxItemsByStatus(OutboxItemStatus status, int maxBatchSize = -1)
        {
            var sql = @$"
                SELECT {(maxBatchSize > 0 ? string.Concat("TOP ", maxBatchSize) : "")}
                    {OutboxTableConfig.UniqueIdentifierFieldName},
                    {OutboxTableConfig.CreatedDateTimeUtcFieldName},
                    {OutboxTableConfig.StatusFieldName},
                    {OutboxTableConfig.PublishingAttemptsFieldName},
                    {OutboxTableConfig.PublishingTargetFieldName},
                    {OutboxTableConfig.PublishingPayloadFieldName}
                FROM
                    [{OutboxTableConfig.TransactionalOutboxSchemaName}].[{OutboxTableConfig.TransactionalOutboxTableName}]
                WHERE
                   {OutboxTableConfig.StatusFieldName} = @status
            ";

            return sql;
        }

        #region Internal Helpers

        public string SanitizeSqlName(string name)
        {
            return name.Trim('[', ']', ' ');
        }

        #endregion
    }
}
