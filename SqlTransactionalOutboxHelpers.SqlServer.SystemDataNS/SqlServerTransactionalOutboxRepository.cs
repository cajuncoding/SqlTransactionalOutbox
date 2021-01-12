using System;
using System.Linq;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using SqlAppLockHelper.SystemDataNS;
using SqlTransactionalOutboxHelpers.CustomExtensions;

namespace SqlTransactionalOutboxHelpers.SqlServer.SystemDataNS
{
    public class SqlServerTransactionalOutboxRepository : BaseSqlServerTransactionalOutboxRepository, ISqlTransactionalOutboxRepository
    {
        protected SqlTransaction SqlTransaction { get; set; }
        protected SqlConnection SqlConnection { get; set; }

        public SqlServerTransactionalOutboxRepository(
            SqlTransaction sqlTransaction, 
            ISqlTransactionalOutboxTableConfig outboxTableConfig = null,
            ISqlTransactionalOutboxItemFactory outboxItemFactory = null,
            int distributedMutexAcquisitionTimeoutSeconds = 5
        )
        {
            SqlTransaction = sqlTransaction ?? 
                throw new ArgumentNullException(nameof(sqlTransaction), "A valid Sql Transaction must be provided for Sql Transactional Outbox processing.");

            SqlConnection = sqlTransaction.Connection ?? 
                throw new ArgumentNullException(nameof(SqlConnection), "The SqlTransaction specified must have a valid SqlConnection.");

            base.Init(outboxTableConfig, outboxItemFactory, distributedMutexAcquisitionTimeoutSeconds);
        }

        public virtual async Task<List<ISqlTransactionalOutboxItem>> RetrieveOutboxItemsAsync(OutboxItemStatus status, int maxBatchSize = -1)
        {
            var statusParamName = "status";
            var sql = QueryBuilder.BuildSqlForRetrieveOutboxItemsByStatus(status, maxBatchSize, statusParamName);
            
            await using var sqlCmd = CreateSqlCommand(sql);
            AddParam(sqlCmd, statusParamName, status.ToString());

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

        public virtual async Task CleanupOutboxHistoricalItemsAsync(TimeSpan historyTimeToKeepTimeSpan)
        {
            var purgeHistoryParamName = "@purgeHistoryBeforeDate";
            var purgeHistoryBeforeDate = DateTime.UtcNow.Subtract(historyTimeToKeepTimeSpan);

            var sql = QueryBuilder.BuildSqlForHistoricalOutboxCleanup(purgeHistoryParamName);
            
            await using var sqlCmd = CreateSqlCommand(sql);
            AddParam(sqlCmd, purgeHistoryParamName, purgeHistoryBeforeDate);

            await sqlCmd.ExecuteNonQueryAsync();
        }

        public virtual async Task InsertNewOutboxItemAsync(ISqlTransactionalOutboxItem outboxItem)
        {
            var sql = QueryBuilder.BuildSqlForInsertNewOutboxItem();

            await using var sqlCmd = CreateSqlCommand(sql);
            AddParam(sqlCmd, OutboxTableConfig.UniqueIdentifierFieldName, outboxItem.UniqueIdentifier);
            AddParam(sqlCmd, OutboxTableConfig.CreatedDateTimeUtcFieldName, outboxItem.CreatedDateTimeUtc);
            AddParam(sqlCmd, OutboxTableConfig.StatusFieldName, outboxItem.Status);
            AddParam(sqlCmd, OutboxTableConfig.PublishingAttemptsFieldName, outboxItem.PublishingAttempts);
            AddParam(sqlCmd, OutboxTableConfig.PublishingTargetFieldName, outboxItem.PublishingTarget);
            AddParam(sqlCmd, OutboxTableConfig.PublishingPayloadFieldName, outboxItem.PublishingPayload);

            await sqlCmd.ExecuteNonQueryAsync();
        }

        public virtual async Task UpdateOutboxItemsAsync(List<ISqlTransactionalOutboxItem> outboxItems, int updateBatchSize = 20)
        {
            await using var sqlCmd = CreateSqlCommand("");

            var batches = outboxItems.Chunk(updateBatchSize);
            foreach (var batch in batches)
            {
                var batchIndex = 0;
                var itemSqlList = new List<string>();
                foreach (var outboxItem in batch)
                {
                    itemSqlList.Add(QueryBuilder.BuildSqlForInsertNewOutboxItem(batchIndex));

                    //NOTE: The only Updateable Fields are Status & PublishingAttempts
                    AddParam(sqlCmd, OutboxTableConfig.StatusFieldName, outboxItem.Status, batchIndex);
                    AddParam(sqlCmd, OutboxTableConfig.PublishingAttemptsFieldName, outboxItem.PublishingAttempts, batchIndex);
                    batchIndex++;
                }

                sqlCmd.CommandText = string.Join($"; {Environment.NewLine}", itemSqlList); ;
                await sqlCmd.ExecuteNonQueryAsync();
            }

        }

        public virtual async Task<IAsyncDisposable> AcquireDistributedProcessingMutexAsync()
        {
            var distributedMutex = await SqlTransaction.AcquireAppLockAsync(
                DistributedMutexLockName, 
                DistributedMutexAcquisitionTimeoutSeconds,
                throwsException: false
            );

            //Safely return null if the Lock was not successfully acquired.
            return distributedMutex.IsLockAcquired ? distributedMutex : null;
        }

        #region Helpers
        
        protected SqlCommand CreateSqlCommand(string sqlCmdText)
        {
            var sqlCmd = this.SqlConnection.CreateCommand();
            sqlCmd.CommandType = CommandType.Text;
            sqlCmd.CommandText = sqlCmdText;
            return sqlCmd;
        }

        protected void AddParam(SqlCommand sqlCmd, string name, object value, int index = -1)
        {
            sqlCmd.Parameters.AddWithValue(
                QueryBuilder.ToSqlParamName(name, index),
                value
            );
        }

        #endregion

    }
}
