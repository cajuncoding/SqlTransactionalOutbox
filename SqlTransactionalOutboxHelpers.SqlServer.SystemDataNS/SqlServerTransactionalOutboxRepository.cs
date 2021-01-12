using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using SqlTransactionalOutboxHelpers.CustomExtensions;

namespace SqlTransactionalOutboxHelpers.SqlServer.SystemDataNS
{
    public class SqlServerTransactionalOutboxRepository : ISqlTransactionalOutboxRepository
    {
        protected SqlTransaction SqlTransaction { get; set; }
        protected SqlConnection SqlConnection { get; set; }
        protected ISqlTransactionalOutboxTableConfig OutboxTableConfig { get; set; }
        protected SqlServerTransactionalOutboxQueryBuilder QueryBuilder { get; set; }
        protected ISqlTransactionalOutboxItemFactory OutboxItemFactory { get; set; }

        public SqlServerTransactionalOutboxRepository(
            SqlTransaction sqlTransaction, 
            ISqlTransactionalOutboxTableConfig outboxTableConfig = null,
            ISqlTransactionalOutboxItemFactory outboxItemFactory = null
        )
        {
            SqlTransaction = sqlTransaction ?? 
                throw new ArgumentNullException(nameof(sqlTransaction), "A valid Sql Transaction must be provided for Sql Transactional Outbox processing.");

            SqlConnection = sqlTransaction.Connection ?? 
                throw new ArgumentNullException(nameof(SqlConnection), "The SqlTransaction specified must have a valid SqlConnection.");

            OutboxTableConfig = outboxTableConfig.AssertNotNull(nameof(outboxTableConfig));
            QueryBuilder = new SqlServerTransactionalOutboxQueryBuilder(outboxTableConfig);
            OutboxItemFactory = outboxItemFactory ?? new OutboxItemFactory();
        }

        public virtual async Task<List<ISqlTransactionalOutboxItem>> RetrieveOutboxItemsAsync(OutboxItemStatus status, int maxBatchSize = -1)
        {
            await using var sqlCmd = this.SqlConnection.CreateCommand();
            sqlCmd.CommandType = CommandType.Text;
            sqlCmd.CommandText = QueryBuilder.BuildSqlForRetrieveOutboxItemsByStatus(status, maxBatchSize);
            sqlCmd.Parameters.AddWithValue("@status", status.ToString());

            var results = new List<ISqlTransactionalOutboxItem>();

            await using var sqlReader = await sqlCmd.ExecuteReaderAsync();
            while (await sqlReader.ReadAsync())
            {
                var outboxItem = OutboxItemFactory.CreateExistingOutboxItem(
                    uniqueIdentifier: (string)sqlReader[OutboxTableConfig.UniqueIdentifierFieldName],
                    status:(string)sqlReader[OutboxTableConfig.StatusFieldName],
                    publishingAttempts:(int)sqlReader[OutboxTableConfig.PublishingAttemptsFieldName],
                    createdDateTimeUtc:(DateTime)sqlReader[OutboxTableConfig.CreatedDateTimeUtcFieldName],
                    publishingTarget:(string)sqlReader[OutboxTableConfig.PublishingTargetFieldName],
                    publishingPayload:(string)sqlReader[OutboxTableConfig.PublishingPayloadFieldName]
                );

                results.Add(outboxItem);
            }

            return results;
        }

        public virtual Task CleanupOutboxHistoricalItemsAsync(TimeSpan historyTimeToKeepTimeSpan)
        {
            throw new NotImplementedException();
        }

        public virtual async Task InsertNewOutboxItemAsync(ISqlTransactionalOutboxItem outboxItem)
        {
            throw new NotImplementedException();
        }

        public virtual async Task UpdateOutboxItemsAsync(List<ISqlTransactionalOutboxItem> outboxItem)
        {
            throw new NotImplementedException();
        }

        public virtual async Task<IAsyncDisposable> AcquireDistributedProcessingMutexAsync()
        {
            throw new NotImplementedException();
        }



    }
}
