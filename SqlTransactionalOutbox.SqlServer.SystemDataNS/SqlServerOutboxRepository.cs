﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SqlAppLockHelper.SystemDataNS;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox.SqlServer.SystemDataNS
{
    public class SqlServerOutboxRepository<TUniqueIdentifier, TPayload> 
        : BaseSqlServerTransactionalOutboxRepository<TUniqueIdentifier, TPayload>, ISqlTransactionalOutboxRepository<TUniqueIdentifier, TPayload>
    {
        protected SqlTransaction SqlTransaction { get; set; }
        protected SqlConnection SqlConnection { get; set; }

        public SqlServerOutboxRepository(
            SqlTransaction sqlTransaction, 
            ISqlTransactionalOutboxTableConfig outboxTableConfig = null,
            ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> outboxItemFactory = null,
            int distributedMutexAcquisitionTimeoutSeconds = 5
        )
        {
            SqlTransaction = sqlTransaction ?? 
                throw new ArgumentNullException(nameof(sqlTransaction), "A valid SqlTransaction must be provided for Sql Transactional Outbox processing.");

            SqlConnection = sqlTransaction.Connection ?? 
                throw new ArgumentNullException(nameof(SqlConnection), "The SqlTransaction specified must have a valid SqlConnection.");

            base.Init(
                outboxTableConfig: outboxTableConfig.AssertNotNull(nameof(outboxTableConfig)), 
                outboxItemFactory: outboxItemFactory.AssertNotNull(nameof(outboxItemFactory)), 
                distributedMutexAcquisitionTimeoutSeconds
            );
        }

        public virtual async Task<List<ISqlTransactionalOutboxItem<TUniqueIdentifier>>> RetrieveOutboxItemsAsync(
            OutboxItemStatus status, 
            int maxBatchSize = -1
        )
        {
            var statusParamName = OutboxTableConfig.StatusFieldName;
            var sql = QueryBuilder.BuildSqlForRetrieveOutboxItemsByStatus(status, maxBatchSize, statusParamName);
            
            await using var sqlCmd = CreateSqlCommand(sql);
            AddParam(sqlCmd, statusParamName, status.ToString(), SqlDbType.VarChar);

            var results = new List<ISqlTransactionalOutboxItem<TUniqueIdentifier>>();

            await using var sqlReader = await sqlCmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await sqlReader.ReadAsync().ConfigureAwait(false))
            {
                var outboxItem = OutboxItemFactory.CreateExistingOutboxItem(
                    uniqueIdentifier: (string)sqlReader[OutboxTableConfig.UniqueIdentifierFieldName],
                    status:(string)sqlReader[OutboxTableConfig.StatusFieldName],
                    publishingAttempts:(int)sqlReader[OutboxTableConfig.PublishingAttemptsFieldName],
                    createdDateTimeUtc:(DateTime)sqlReader[OutboxTableConfig.CreatedDateTimeUtcFieldName],
                    publishingTarget:(string)sqlReader[OutboxTableConfig.PublishingTargetFieldName],
                    serializedPayload:(string)sqlReader[OutboxTableConfig.PublishingPayloadFieldName]
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
            AddParam(sqlCmd, purgeHistoryParamName, purgeHistoryBeforeDate, SqlDbType.DateTime2);

            await sqlCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public virtual async Task<List<ISqlTransactionalOutboxItem<TUniqueIdentifier>>> InsertNewOutboxItemsAsync(
            IEnumerable<ISqlTransactionalOutboxInsertionItem<TPayload>> outboxItems, 
            int insertBatchSize = 20
        )
        {
            await using var sqlCmd = CreateSqlCommand("");

            //Use the Outbox Item Factory to create a new Outbox Item with serialized payload.
            var outboxItemsList = outboxItems.Select(
                i => OutboxItemFactory.CreateNewOutboxItem(
                    i.PublishingTarget, 
                    i.PublishingPayload
                )
            ).ToList();

            var batches = outboxItemsList.Chunk(insertBatchSize);
            foreach (var batch in batches)
            {
                sqlCmd.CommandText = QueryBuilder.BuildParameterizedSqlToInsertNewOutboxItems(batch);
                sqlCmd.Parameters.Clear();

                //Add the Parameters!
                for (var batchIndex = 0; batchIndex < batch.Length; batchIndex++)
                {
                    var outboxItem = batch[batchIndex];

                    var uniqueIdentifierForDb = ConvertUniqueIdentifierForDb(outboxItem.UniqueIdentifier);
                    AddParam(sqlCmd, OutboxTableConfig.UniqueIdentifierFieldName, uniqueIdentifierForDb, SqlDbType.UniqueIdentifier, batchIndex);

                    //NOTE: The for Sql Server, the CreatedDateTimeUtcField is automatically populated by Sql Server.
                    //      this helps eliminate risks of datetime sequencing across servers or server-less environments.
                    //AddParam(sqlCmd, OutboxTableConfig.CreatedDateTimeUtcFieldName, outboxItem.CreatedDateTimeUtc, batchIndex);
                    AddParam(sqlCmd, OutboxTableConfig.StatusFieldName, outboxItem.Status.ToString(), SqlDbType.VarChar, batchIndex);
                    AddParam(sqlCmd, OutboxTableConfig.PublishingAttemptsFieldName, outboxItem.PublishingAttempts, SqlDbType.Int, batchIndex);
                    AddParam(sqlCmd, OutboxTableConfig.PublishingTargetFieldName, outboxItem.PublishingTarget, SqlDbType.VarChar, batchIndex);
                    AddParam(sqlCmd, OutboxTableConfig.PublishingPayloadFieldName, outboxItem.PublishingPayload, SqlDbType.NVarChar, batchIndex);
                }

                //Execute the Batch and continue...
                await using var sqlReader = await sqlCmd.ExecuteReaderAsync().ConfigureAwait(false);

                //Since some fields are actually populated in the Database, we post-process to update the models with valid
                //  values as returned from teh Insert process...
                var outboxBatchLookup = batch.ToLookup(i => i.UniqueIdentifier);
                while (await sqlReader.ReadAsync().ConfigureAwait(false))
                {
                    var uniqueIdentifier = ConvertUniqueIdentifierFromDb(sqlReader);
                    var outboxItem = outboxBatchLookup[uniqueIdentifier].First();

                    var createdDateUtcFromDb = (DateTime)sqlReader[OutboxTableConfig.CreatedDateTimeUtcFieldName];
                    outboxItem.CreatedDateTimeUtc = createdDateUtcFromDb;
                }
            }

            return outboxItemsList;
        }

        public virtual async Task<List<ISqlTransactionalOutboxItem<TUniqueIdentifier>>> UpdateOutboxItemsAsync(
            IEnumerable<ISqlTransactionalOutboxItem<TUniqueIdentifier>> outboxItems, 
            int updateBatchSize = 20
        )
        {
            await using var sqlCmd = CreateSqlCommand("");

            var outboxItemsList = outboxItems.ToList();

            var batches = outboxItemsList.Chunk(updateBatchSize);
            foreach (var batch in batches)
            {
                sqlCmd.CommandText = QueryBuilder.BuildParameterizedSqlToUpdateExistingOutboxItem(batch);
                sqlCmd.Parameters.Clear();

                //Add the Parameters!
                for (var batchIndex = 0; batchIndex < batch.Length; batchIndex++)
                {
                    var outboxItem = batch[batchIndex];

                    //Unique Identifier is used for Identification Match!
                    var uniqueIdentifierForDb = ConvertUniqueIdentifierForDb(outboxItem.UniqueIdentifier);
                    AddParam(sqlCmd, OutboxTableConfig.UniqueIdentifierFieldName, uniqueIdentifierForDb, SqlDbType.UniqueIdentifier, batchIndex);

                    //NOTE: The only Updateable Fields are Status & PublishingAttempts
                    AddParam(sqlCmd, OutboxTableConfig.StatusFieldName, outboxItem.Status.ToString(), SqlDbType.VarChar, batchIndex);
                    AddParam(sqlCmd, OutboxTableConfig.PublishingAttemptsFieldName, outboxItem.PublishingAttempts, SqlDbType.Int, batchIndex);
                }

                //Execute the Batch and continue...
                await sqlCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            return outboxItemsList;
        }

        /// <summary>
        /// Provide virtual converter functionality, that can be overridden as needed, to help isolate the conversion
        /// of UniqueIdentifier values to the correct format from the Model to the Database Schema.  Default behavior
        /// assumes that it's a type that can be intrinsically cast without special conversion (e.g. Guid, Int, etc.).
        /// </summary>
        /// <param name="sqlReader"></param>
        /// <returns></returns>
        public virtual TUniqueIdentifier ConvertUniqueIdentifierFromDb(SqlDataReader sqlReader)
        {
            TUniqueIdentifier uniqueIdentifier = (TUniqueIdentifier)sqlReader[OutboxTableConfig.UniqueIdentifierFieldName];
            return uniqueIdentifier;
        }

        /// <summary>
        /// Provide virtual converter functionality, that can be overridden as needed, to help isolate the conversion
        /// of UniqueIdentifier values to the correct format from the Database Schema to the Model.  Default behavior
        /// assumes that it's a type that can be intrinsically cast without special conversion (e.g. Guid, Int, etc.).
        /// </summary>
        /// <param name="uniqueIdentifier"></param>
        /// <returns></returns>
        public virtual object ConvertUniqueIdentifierForDb(TUniqueIdentifier uniqueIdentifier)
        {
            object uniqueIdentifierForDb = (object)uniqueIdentifier;
            return uniqueIdentifierForDb;
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
        
        protected virtual SqlCommand CreateSqlCommand(string sqlCmdText)
        {
            var sqlCmd = new SqlCommand(sqlCmdText, this.SqlConnection, this.SqlTransaction)
            {
                CommandType = CommandType.Text
            };
            return sqlCmd;
        }

        protected virtual void AddParam(SqlCommand sqlCmd, string name, object value, SqlDbType dbType, int index = -1)
        {
            var paramName = QueryBuilder.ToSqlParamName(name, index);

            //Attempt to optimize the details for NVarChar(MAX) field for payload inserts/updates by specifying
            //the Size = -1 to force MAX size usage in the SqlClient parameter binding...
            if (name.Equals(OutboxTableConfig.PublishingPayloadFieldName, StringComparison.OrdinalIgnoreCase))
            {
                sqlCmd.Parameters.Add(paramName, SqlDbType.NVarChar, -1).Value = value;
            }
            else
            {
                sqlCmd.Parameters.Add(paramName, dbType).Value = value;
            }
        }

        #endregion

    }
}