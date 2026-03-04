using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox.SqlServer.Common
{
    public class SqlServerTransactionalOutboxQueryBuilder<TUniqueIdentifier>
    {
        public const string DefaultOutboxStatusParamName = "OutboxStatus";

        public ISqlTransactionalOutboxTableConfig OutboxTableConfig { get; protected set; }

        public SqlServerTransactionalOutboxQueryBuilder(ISqlTransactionalOutboxTableConfig outboxTableConfig)
        {
            this.OutboxTableConfig = outboxTableConfig.AssertNotNull(nameof(outboxTableConfig));
        }

        public virtual string BuildSqlForRetrieveOutboxItemsByStatus(
            OutboxItemStatus status,
            int maxBatchSize = -1,
            TimeSpan? scheduledPublishPrefetchTime = null,
            string statusParamName = DefaultOutboxStatusParamName
        )
        {
            var sanitizedScheduledPrefetchTime = scheduledPublishPrefetchTime ?? TimeSpan.Zero;
            var scheduledPublishDateTimeField = ToSqlFieldName(OutboxTableConfig.ScheduledPublishDateTimeUtcFieldName);
            var statusField = ToSqlFieldName(OutboxTableConfig.StatusFieldName);

            return @$"
                SELECT {(maxBatchSize > 0 ? string.Concat("TOP ", maxBatchSize) : string.Empty)}
                    {ToSqlFieldName(OutboxTableConfig.UniqueIdentifierFieldName)},
                    {ToSqlFieldName(OutboxTableConfig.FifoGroupingIdentifier)},
                    {ToSqlFieldName(OutboxTableConfig.CreatedDateTimeUtcFieldName)},
                    {scheduledPublishDateTimeField},
                    {statusField},
                    {ToSqlFieldName(OutboxTableConfig.PublishAttemptsFieldName)},
                    {ToSqlFieldName(OutboxTableConfig.PublishTargetFieldName)},
                    {ToSqlFieldName(OutboxTableConfig.PayloadFieldName)}
                FROM
                    {BuildTableName()}
                WHERE
                    {statusField} = {ToSqlParamName(statusParamName)}
                    {status switch {
                        //When PENDING we need to factor in Schedule Times!
                        //NOTE: This has no bearing on othe statuses such as Successful, Failed, Expired, etc. which should be retrieved regardless
                        //      of schedule time since they are no longer pending items waiting to be published; but rather historical items that need
                        //      to be retrieved for other purposes such as cleanup, monitoring, auditing, etc.
                        //NOTE: Only include Non - scheduled results or results with schedule time that has passed with the provided prefetch time
                        //      to allow for early retrieval before the exact scheduled time -- by adding the prefetch time (if specified) to 
                        //      the Current System UTC time effectively looking into the future for scheduled items to include also...
                        OutboxItemStatus.Pending => @$"AND (
                            {scheduledPublishDateTimeField} IS NULL
                            OR {sanitizedScheduledPrefetchTime switch {
                                //NOTE: We ADD the Prefetch Time to the Current UTC Time to effectively look into the future & pre-fetching scheduled items before their exact scheduled time.
                                _ when sanitizedScheduledPrefetchTime > TimeSpan.Zero => $"{scheduledPublishDateTimeField} <= DateAdd(SECOND, {sanitizedScheduledPrefetchTime.TotalSeconds}, SysUtcDateTime())",
                                _ => $"{scheduledPublishDateTimeField} <= SysUtcDateTime()",
                            }}
                        )",
                        _ => string.Empty
                    }}
                ORDER BY
                    --Order by Created DateTime, and break any collisions using the PKey field (IDENTITY).
                    {ToSqlFieldName(OutboxTableConfig.CreatedDateTimeUtcFieldName)},
                    {ToSqlFieldName(OutboxTableConfig.ScheduledPublishDateTimeUtcFieldName)},
                    {ToSqlFieldName(OutboxTableConfig.PKeyFieldName)};
            ";
        }

        public virtual string BuildSqlForHistoricalOutboxCleanup(TimeSpan historyTimeToKeepTimeSpan)
        {
            var publishScheduledField = ToSqlFieldName(OutboxTableConfig.ScheduledPublishDateTimeUtcFieldName);
            var historyToKeepSeconds = -1 * historyTimeToKeepTimeSpan.TotalSeconds;

            return @$"
                DELETE FROM {BuildTableName()}
                WHERE
                    {ToSqlFieldName(OutboxTableConfig.CreatedDateTimeUtcFieldName)} < DateAdd(SECOND, {historyToKeepSeconds}, SysUtcDateTime())
                    AND (
                        {publishScheduledField} IS NULL
                        OR {publishScheduledField} < DateAdd(SECOND, {historyToKeepSeconds}, SysUtcDateTime())
                    )
            ";
        }

        public virtual string BuildSqlForBulkPublishAttemptsIncrementByStatus(string statusParamName = DefaultOutboxStatusParamName)
        {
            var publishAttemptsField = ToSqlFieldName(OutboxTableConfig.PublishAttemptsFieldName);
            return @$"
                UPDATE {BuildTableName()}
                SET {publishAttemptsField} = ({publishAttemptsField} + 1)
                WHERE {ToSqlFieldName(OutboxTableConfig.StatusFieldName)} = {ToSqlParamName(statusParamName)};
            ";
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
                {ToSqlFieldName(OutboxTableConfig.FifoGroupingIdentifier)},
                {ToSqlFieldName(OutboxTableConfig.StatusFieldName)},
                {ToSqlFieldName(OutboxTableConfig.CreatedDateTimeUtcFieldName)},
                {ToSqlFieldName(OutboxTableConfig.ScheduledPublishDateTimeUtcFieldName)},
                {ToSqlFieldName(OutboxTableConfig.PublishAttemptsFieldName)},
                {ToSqlFieldName(OutboxTableConfig.PublishTargetFieldName)},
                {ToSqlFieldName(OutboxTableConfig.PayloadFieldName)}
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
                        {ToSqlParamName(OutboxTableConfig.FifoGroupingIdentifier, index)},
                        {ToSqlParamName(OutboxTableConfig.StatusFieldName, index)},
                        SysUtcDateTime(), --Auto Populate [CreatedDateTimeUtc]!
                        {ToSqlParamName(OutboxTableConfig.ScheduledPublishDateTimeUtcFieldName, index)},
                        {ToSqlParamName(OutboxTableConfig.PublishAttemptsFieldName, index)},
                        {ToSqlParamName(OutboxTableConfig.PublishTargetFieldName, index)},
                        {ToSqlParamName(OutboxTableConfig.PayloadFieldName, index)},
                        {index} --[SortOrdinal] Temp Table field guarantees proper ordering!
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
            var publishingAttemptsFieldName = ToSqlFieldName(OutboxTableConfig.PublishAttemptsFieldName);

            var sqlStringBuilder = new StringBuilder();
            var itemCount = outboxItems.Count();

            for (var index = 0; index < itemCount; index++)
            {
                sqlStringBuilder.Append(@$"
                    UPDATE {tableName} SET
                        {statusFieldName} = {ToSqlParamName(OutboxTableConfig.StatusFieldName, index)},
                        {publishingAttemptsFieldName} = {ToSqlParamName(OutboxTableConfig.PublishAttemptsFieldName, index)}
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
