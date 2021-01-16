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
                throw new ArgumentNullException(nameof(sqlTransaction), "A valid SqlTransaction must be provided for Sql Transactional Outbox processing.");

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

            await using var sqlReader = await sqlCmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await sqlReader.ReadAsync().ConfigureAwait(false))
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

            await sqlCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public virtual async Task<IEnumerable<ISqlTransactionalOutboxItem>> InsertNewOutboxItemsAsync(IEnumerable<ISqlTransactionalOutboxItem> outboxItems, int insertBatchSize = 20)
        {
            await using var sqlCmd = CreateSqlCommand("");
            
            var outboxItemsList = outboxItems.ToList();

            var batches = outboxItemsList.Chunk(insertBatchSize);
            foreach (var batch in batches)
            {
                sqlCmd.CommandText = QueryBuilder.BuildParameterizedSqlToInsertNewOutboxItems(batch);

                //Add the Parameters!
                var batchIndex = 0;
                foreach (var outboxItem in batch)
                {
                    //NOTE: The for Sql Server, the CreatedDateTimeUtcField is automatically populated by Sql Server,
                    //      so we skip it here!
                    //NOTE: This is because the value is critical to enforcing FIFO processing we use the centralized
                    //      database as the source of getting highly precise Utc DateTime values for all data inserted.
                    //NOTE: UTC Created DateTime will be automatically populated by DEFAULT constraint using
                    //      SysUtcDateTime() per the Sql Server table script; SysUtcDateTime() is Sql Server's
                    //      implementation for highly precise values stored as datetime2 fields.
                    AddParam(sqlCmd, OutboxTableConfig.UniqueIdentifierFieldName, outboxItem.UniqueIdentifier, batchIndex);
                    //AddParam(sqlCmd, OutboxTableConfig.CreatedDateTimeUtcFieldName, outboxItem.CreatedDateTimeUtc, batchIndex);
                    AddParam(sqlCmd, OutboxTableConfig.StatusFieldName, outboxItem.Status, batchIndex);
                    AddParam(sqlCmd, OutboxTableConfig.PublishingAttemptsFieldName, outboxItem.PublishingAttempts, batchIndex);
                    AddParam(sqlCmd, OutboxTableConfig.PublishingTargetFieldName, outboxItem.PublishingTarget, batchIndex);
                    AddParam(sqlCmd, OutboxTableConfig.PublishingPayloadFieldName, outboxItem.PublishingPayload, batchIndex);
                    batchIndex++;
                }

                //Execute the Batch and continue...
                var sqlReader = await sqlCmd.ExecuteReaderAsync().ConfigureAwait(false);

                //Since some fields are actually populated in the Database, we post-process to update the models with valid
                //  values as returned from teh Insert process...
                var outboxBatchLookup = batch.ToLookup(i => i.UniqueIdentifier);
                while (await sqlReader.ReadAsync().ConfigureAwait(false))
                {
                    //The First field is always our UniqueIdentifier (as defined by the Output clause of the Sql)
                    // and the Second field is always the UTC Created DateTime returned from the Database.
                    var uniqueIdentifier = sqlReader.GetString(0);
                    var createdDateUtcFromDb = sqlReader.GetDateTime(1);

                    var outboxItem = outboxBatchLookup[uniqueIdentifier].First();
                    outboxItem.CreatedDateTimeUtc = createdDateUtcFromDb;
                }
            }

            return outboxItemsList;
        }

        public virtual async Task<IEnumerable<ISqlTransactionalOutboxItem>> UpdateOutboxItemsAsync(IEnumerable<ISqlTransactionalOutboxItem> outboxItems, int updateBatchSize = 20)
        {
            await using var sqlCmd = CreateSqlCommand("");

            var outboxItemsList = outboxItems.ToList();

            var batches = outboxItemsList.Chunk(updateBatchSize);
            foreach (var batch in batches)
            {
                sqlCmd.CommandText = QueryBuilder.BuildParameterizedSqlToUpdateExistingOutboxItem(batch);

                //Add the Parameters!
                var batchIndex = 0;
                foreach (var outboxItem in batch)
                {
                   //NOTE: The only Updateable Fields are Status & PublishingAttempts
                    AddParam(sqlCmd, OutboxTableConfig.StatusFieldName, outboxItem.Status, batchIndex);
                    AddParam(sqlCmd, OutboxTableConfig.PublishingAttemptsFieldName, outboxItem.PublishingAttempts, batchIndex);
                    batchIndex++;
                }

                //Execute the Batch and continue...
                await sqlCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            return outboxItemsList;
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
