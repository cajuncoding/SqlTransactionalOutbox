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
                   {ToSqlFieldName(OutboxTableConfig.StatusFieldName)} = {ToSqlParamName(statusParamName)}
                ORDER BY
                    --Order by Created DateTime, and break any collisions using the PKey field (IDENTITY).
                    {ToSqlFieldName(OutboxTableConfig.CreatedDateTimeUtcFieldName)},
                    {ToSqlFieldName(OutboxTableConfig.PKeyFieldName)};
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
        ///       the value is critical to enforcing FIFO processing. Instead
        ///       we use the DateTime the centralized database as the source to help ensure
        ///       integrity & precision of the item entry order.
        /// NOTE: UTC Created DateTime will be automatically populated by datetime generated
        ///       at the database level for continuity of time to eliminate risk of DateTime sequencing
        ///       across servers or server-less environments.
        /// NOTE: SysUtcDateTime() is the Sql Server implementation for highly precise date time values
        ///       stored as datetime2 with maximum support for fractional seconds; but this will not be
        ///       Unique when many items are stored at the same time in bulk.
        /// NOTE: Therefore the IDENTITY Id column is used as the Secondary Sort to break any ties,
        ///       while also providing an efficient PKey for Sql Server; though the IDENTITY Value
        ///       is a DB Specific use and is never returned as part of the Outbox Item.
        /// </summary>
        /// <param name="outboxItems"></param>
        /// <returns></returns>
        public virtual string BuildParameterizedSqlToInsertNewOutboxItems(
            IEnumerable<ISqlTransactionalOutboxItem<TUniqueIdentifier>> outboxItems
        )
        {
            var sqlStringBuilder = new StringBuilder();

            var sqlTransactionalOutboxTableFieldNames = @$"
                    {ToSqlFieldName(OutboxTableConfig.UniqueIdentifierFieldName)},
                    {ToSqlFieldName(OutboxTableConfig.StatusFieldName)},
                    {ToSqlFieldName(OutboxTableConfig.CreatedDateTimeUtcFieldName)},                    
                    {ToSqlFieldName(OutboxTableConfig.PublishingAttemptsFieldName)},
                    {ToSqlFieldName(OutboxTableConfig.PublishingTargetFieldName)},
                    {ToSqlFieldName(OutboxTableConfig.PublishingPayloadFieldName)}
            ";

            sqlStringBuilder.Append(@$"
                INSERT INTO {BuildTableName()} ({sqlTransactionalOutboxTableFieldNames})
                OUTPUT
                    INSERTED.{ToSqlFieldName(OutboxTableConfig.UniqueIdentifierFieldName)},
                    INSERTED.{ToSqlFieldName(OutboxTableConfig.CreatedDateTimeUtcFieldName)}
                SELECT {sqlTransactionalOutboxTableFieldNames}
                FROM (                
                    VALUES"
            );

            var itemCount = outboxItems.Count();
            for (var index = 0; index < itemCount; index++)
            {
                //Comma delimit the VALUES sets...
                if (index > 0) sqlStringBuilder.Append(",");

                sqlStringBuilder.Append(@$"
                    (
                        {ToSqlParamName(OutboxTableConfig.UniqueIdentifierFieldName, index)},
                        {ToSqlParamName(OutboxTableConfig.StatusFieldName, index)},
                        SysUtcDateTime(),
                        {ToSqlParamName(OutboxTableConfig.PublishingAttemptsFieldName, index)},
                        {ToSqlParamName(OutboxTableConfig.PublishingTargetFieldName, index)},
                        {ToSqlParamName(OutboxTableConfig.PublishingPayloadFieldName, index)},
                        {index}
                    )
                ");
            }

            sqlStringBuilder.Append(@$"
                ) AS TempTable (
                    {sqlTransactionalOutboxTableFieldNames}, 
                    [SortOrdinal]
                )
                --The Order By clause guarantees that IDENTITY values will be assigned in the Order we Specify!
                ORDER BY [SortOrdinal] ASC;
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
            if (_fullyQualifiedTableName == null)
            {
                var schemaName = ToSqlFieldName(OutboxTableConfig.TransactionalOutboxSchemaName);
                var tableName = ToSqlFieldName(OutboxTableConfig.TransactionalOutboxTableName);
                _fullyQualifiedTableName ??= string.Concat(schemaName, ".", tableName);
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
