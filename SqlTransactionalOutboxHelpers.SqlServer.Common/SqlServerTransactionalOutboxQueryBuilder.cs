using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SqlTransactionalOutboxHelpers
{
    public class SqlServerTransactionalOutboxQueryBuilder<TUniqueIdentifier>
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

        /// <summary>
        /// NOTE: In Sql Server, we ignore date/time values provided (if provided) because
        ///         the value is critical to enforcing FIFO processing. Instead
        ///         we use the DateTime the centralized database as the source.
        /// NOTE: UTC Created DateTime will be automatically populated by DEFAULT constraint using
        ///         SysUtcDateTime() per the Sql Server table script; SysUtcDateTime() is Sql Server's
        ///         implementation for highly precise values stored as datetime2 fields.
        /// </summary>
        /// <param name="outboxItems"></param>
        /// <returns></returns>
        public virtual string BuildParameterizedSqlToInsertNewOutboxItems(
            IEnumerable<ISqlTransactionalOutboxItem<TUniqueIdentifier>> outboxItems
        )
        {
            var itemCount = outboxItems.Count();
            var sqlStringBuilder = new StringBuilder();

            sqlStringBuilder.Append(@$"
                INSERT INTO {BuildTableName()} (
                    {ToSqlFieldName(OutboxTableConfig.UniqueIdentifierFieldName)},
                    {ToSqlFieldName(OutboxTableConfig.StatusFieldName)},
                    {ToSqlFieldName(OutboxTableConfig.PublishingAttemptsFieldName)},
                    {ToSqlFieldName(OutboxTableConfig.PublishingTargetFieldName)},
                    {ToSqlFieldName(OutboxTableConfig.PublishingPayloadFieldName)}
                ) 
                VALUES"
            );

            for (var index = 0; index < itemCount; index++)
            {
                //NOTE: In Sql Server, we ignore date/time values provided (if provided) because
                //      the value is critical to enforcing FIFO processing. Instead
                //      we use the DateTime the centralized database as the source to help ensure
                //      integrity & precision of the item entry order.
                //NOTE: UTC Created DateTime will be automatically populated by DEFAULT constraint using
                //      SysUtcDateTime() per the Sql Server table script; SysUtcDateTime() is Sql Server's
                //      implementation for highly precise values stored as datetime2 fields.
                sqlStringBuilder.Append(@$"
                (
                    {ToSqlParamName(OutboxTableConfig.UniqueIdentifierFieldName, index)},
                    {ToSqlParamName(OutboxTableConfig.StatusFieldName, index)},
                    {ToSqlParamName(OutboxTableConfig.PublishingAttemptsFieldName, index)},
                    {ToSqlParamName(OutboxTableConfig.PublishingTargetFieldName, index)},
                    {ToSqlParamName(OutboxTableConfig.PublishingPayloadFieldName, index)}
                )");
            }

            //Append the OUTPUT so that we can return data initialized at the Database Layer!
            //As noted above the UTC Created Date Time will be populated by Sql Server DEFAULT value
            //  for this field in the table!
            sqlStringBuilder.Append(@$"
                OUTPUT 
                    inserted.{ToSqlFieldName(OutboxTableConfig.UniqueIdentifierFieldName)}, 
                    inserted.{ToSqlFieldName(OutboxTableConfig.CreatedDateTimeUtcFieldName)};
            ");

            return sqlStringBuilder.ToString();
        }

        public virtual string BuildParameterizedSqlToUpdateExistingOutboxItem(
            IEnumerable<ISqlTransactionalOutboxItem<TUniqueIdentifier>> outboxItems
        )
        {
            var uniqueIdentifierFieldName = ToSqlFieldName(OutboxTableConfig.UniqueIdentifierFieldName);
            var tableName = BuildTableName();
            var statusFieldName = ToSqlFieldName(OutboxTableConfig.StatusFieldName);
            var publishingAttemptsFieldName = ToSqlFieldName(OutboxTableConfig.PublishingAttemptsFieldName);

            var sqlStringBuilder = new StringBuilder();
            var itemCount = outboxItems.Count();

            for (var index = 0; index < itemCount; index++)
            {
                sqlStringBuilder.Append(@$"
                    UPDATE {tableName} SET
                        {statusFieldName} = {ToSqlParamName(OutboxTableConfig.StatusFieldName, index)},
                        {publishingAttemptsFieldName} = {ToSqlParamName(OutboxTableConfig.PublishingAttemptsFieldName, index)}
                    WHERE
                        {uniqueIdentifierFieldName} = {ToSqlParamName(OutboxTableConfig.UniqueIdentifierFieldName, index)};
                ");
            }

            return sqlStringBuilder.ToString();
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
