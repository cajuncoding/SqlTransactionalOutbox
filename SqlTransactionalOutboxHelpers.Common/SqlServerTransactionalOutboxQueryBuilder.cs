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

        public virtual string BuildSqlForRetrieveOutboxItemsByStatus(OutboxItemStatus status, int maxBatchSize = -1, string statusParamName = "status")
        {
            var sql = @$"
                SELECT {(maxBatchSize > 0 ? string.Concat("TOP ", maxBatchSize) : "")}
                    {ToSqlFieldName(OutboxTableConfig.UniqueIdentifierFieldName)},
                    {ToSqlFieldName(OutboxTableConfig.CreatedDateTimeUtcFieldName)},
                    {ToSqlFieldName(OutboxTableConfig.StatusFieldName)},
                    {ToSqlFieldName(OutboxTableConfig.PublishingAttemptsFieldName)},
                    {ToSqlFieldName(OutboxTableConfig.PublishingTargetFieldName)},
                    {ToSqlFieldName(OutboxTableConfig.PublishingPayloadFieldName)}
                FROM
                    {BuildTableName()}
                WHERE
                   {OutboxTableConfig.StatusFieldName} = {ToSqlParamName(statusParamName)};
            ";

            return sql;
        }

        public virtual string BuildSqlForHistoricalOutboxCleanup(string purgeHistoryBeforeDateParamName)
        {
            var sql = @$"
                DELETE FROM {BuildTableName()}
                WHERE {ToSqlFieldName(OutboxTableConfig.CreatedDateTimeUtcFieldName)} < {ToSqlParamName(purgeHistoryBeforeDateParamName)};
            ";

            return sql;
        }

        public virtual string BuildSqlForInsertNewOutboxItem(int index = -1)
        {
            var sql = @$"
                INSERT INTO {BuildTableName()} (
                    {ToSqlFieldName(OutboxTableConfig.UniqueIdentifierFieldName)},
                    {ToSqlFieldName(OutboxTableConfig.CreatedDateTimeUtcFieldName)},
                    {ToSqlFieldName(OutboxTableConfig.StatusFieldName)},
                    {ToSqlFieldName(OutboxTableConfig.PublishingAttemptsFieldName)},
                    {ToSqlFieldName(OutboxTableConfig.PublishingTargetFieldName)},
                    {ToSqlFieldName(OutboxTableConfig.PublishingPayloadFieldName)}
                )
                VALUES (
                    {ToSqlParamName(OutboxTableConfig.UniqueIdentifierFieldName, index)},
                    {ToSqlParamName(OutboxTableConfig.CreatedDateTimeUtcFieldName, index)},
                    {ToSqlParamName(OutboxTableConfig.StatusFieldName, index)},
                    {ToSqlParamName(OutboxTableConfig.PublishingAttemptsFieldName, index)},
                    {ToSqlParamName(OutboxTableConfig.PublishingTargetFieldName, index)},
                    {ToSqlParamName(OutboxTableConfig.PublishingPayloadFieldName, index)}
                );
            ";

            return sql;
        }

        public virtual string BuildSqlForUpdateExistingOutboxItem(int index = -1)
        {
            var sql = @$"
                UPDATE {BuildTableName()} SET
                    {ToSqlFieldName(OutboxTableConfig.StatusFieldName)} = {ToSqlParamName(OutboxTableConfig.StatusFieldName, index)},
                    {ToSqlFieldName(OutboxTableConfig.PublishingAttemptsFieldName)} = {ToSqlParamName(OutboxTableConfig.PublishingAttemptsFieldName, index)}
                WHERE
                    {ToSqlFieldName(OutboxTableConfig.UniqueIdentifierFieldName)} = {ToSqlParamName(OutboxTableConfig.UniqueIdentifierFieldName, index)}
            ";

            return sql;
        }

        #region Internal Helpers

        private string _fullyQualifiedTableName = null;
        public virtual string BuildTableName()
        {
            if (_fullyQualifiedTableName != null)
            {
                var schemaName = ToSqlFieldName(OutboxTableConfig.TransactionalOutboxSchemaName);
                var tableName = ToSqlFieldName(OutboxTableConfig.TransactionalOutboxTableName);
                _fullyQualifiedTableName ??= $"[{schemaName}].[{tableName}]";
            }

            return _fullyQualifiedTableName;
        }

        public virtual string ToSqlFieldName(string name)
        {
            return string.Concat("[", name.Trim('[', ']', ' '), "]");
        }

        public virtual string ToSqlParamName(string name, int ordinal = -1)
        {
            var paramName = string.Concat("@", name.Trim('@', ' '));
            
            if (ordinal >= 0)
                paramName = string.Concat(paramName, "_", ordinal);

            return paramName;
        }

        #endregion
    }
}
