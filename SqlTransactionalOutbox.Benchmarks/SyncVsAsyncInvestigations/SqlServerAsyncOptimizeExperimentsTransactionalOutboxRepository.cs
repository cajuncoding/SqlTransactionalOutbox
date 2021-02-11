//using System;
//using System.Linq;
//using System.Collections.Generic;
//using System.Data;
//using System.Data.SqlClient;
//using System.Threading.Tasks;
//using SqlAppLockHelper.SystemDataNS;
//using SqlTransactionalOutbox.CustomExtensions;

//namespace SqlTransactionalOutbox.SqlServer.SystemDataNS
//{
//    public class SqlServerAsyncOptimizeExperimentsTransactionalOutboxRepository<TPayload> : SqlServerGuidTransactionalOutboxRepository<TPayload>
//    {
//        public SqlServerAsyncOptimizeExperimentsTransactionalOutboxRepository(
//            SqlTransaction sqlTransaction, 
//            ISqlTransactionalOutboxTableConfig outboxTableConfig = null,
//            ISqlTransactionalOutboxItemFactory<Guid, TPayload> outboxItemFactory = null,
//            int distributedMutexAcquisitionTimeoutSeconds = 5
//        )
//        : base(
//            sqlTransaction,
//            outboxTableConfig,
//            outboxItemFactory,
//            distributedMutexAcquisitionTimeoutSeconds
//        )
//        {
//        }

//        public override async Task<List<ISqlTransactionalOutboxItem<Guid>>> RetrieveOutboxItemsAsync(
//            OutboxItemStatus status,
//            int maxBatchSize = -1
//        )
//        {
//            var statusParamName = OutboxTableConfig.StatusFieldName;
//            var sql = QueryBuilder.BuildSqlForRetrieveOutboxItemsByStatus(status, maxBatchSize, statusParamName);

//            await using var sqlCmd = CreateSqlCommand(sql);
//            AddParam(sqlCmd, statusParamName, status.ToString());

//            var results = new List<ISqlTransactionalOutboxItem<Guid>>();

//            await using var sqlReader = await sqlCmd
//                .ExecuteReaderAsync(CommandBehavior.SequentialAccess)
//                .ConfigureAwait(false);

//            //var uniqueIdentifierCol = sqlReader.GetOrdinal(OutboxTableConfig.UniqueIdentifierFieldName);
//            //var statusCol= sqlReader.GetOrdinal(OutboxTableConfig.StatusFieldName);
//            //var publishingAttemptsCol = sqlReader.GetOrdinal(OutboxTableConfig.PublishAttemptsFieldName);
//            //var createdDateUtcCol= sqlReader.GetOrdinal(OutboxTableConfig.CreatedDateTimeUtcFieldName);
//            //var publishingTargetCol = sqlReader.GetOrdinal(OutboxTableConfig.PublishTargetFieldName);
//            //var serializedPayloadCol = sqlReader.GetOrdinal(OutboxTableConfig.PayloadFieldName);

//            while (await sqlReader.ReadAsync().ConfigureAwait(false))
//            {

//                var outboxItem = OutboxItemFactory.CreateExistingOutboxItem(
//                    uniqueIdentifier: sqlReader.GetFieldValue<Guid>(0),
//                    createdDateTimeUtc: sqlReader.GetDateTime(1),
//                    status: sqlReader.GetString(2),
//                    publishingAttempts: sqlReader.GetInt32(3),
//                    publishingTarget: sqlReader.GetString(4),
//                    serializedPayload: (string)await sqlReader.GetTextReader(5).ReadToEndAsync()
//                );

//                results.Add(outboxItem);
//            }

//            return results;
//        }

//        public override async Task<List<ISqlTransactionalOutboxItem<Guid>>> InsertNewOutboxItemsAsync(
//             IEnumerable<ISqlTransactionalOutboxInsertionItem<TPayload>> outboxItems,
//             int insertBatchSize = 20
//         )
//        {
//            await using var sqlCmd = CreateSqlCommand("");

//            //Use the Outbox Item Factory to create a new Outbox Item with serialized payload.
//            var outboxItemsList = outboxItems.Select(
//                i => OutboxItemFactory.CreateNewOutboxItem(
//                    i.PublishTarget,
//                    i.Payload
//                )
//            ).ToList();

//            var batches = outboxItemsList.Chunk(insertBatchSize);
//            foreach (var batch in batches)
//            {
//                sqlCmd.CommandText = QueryBuilder.BuildParameterizedSqlToInsertNewOutboxItems(batch);
//                sqlCmd.Parameters.Clear();

//                //Add the Parameters!
//                for (var batchIndex = 0; batchIndex < batch.Length; batchIndex++)
//                {
//                    var outboxItem = batch[batchIndex];

//                    var uniqueIdentifierForDb = ConvertUniqueIdentifierForDb(outboxItem.UniqueIdentifier);
//                    AddParam(sqlCmd, OutboxTableConfig.UniqueIdentifierFieldName, uniqueIdentifierForDb, batchIndex);

//                    //NOTE: The for Sql Server, the CreatedDateTimeUtcField is automatically populated by Sql Server.
//                    //      this helps eliminate risks of datetime sequencing across servers or server-less environments.
//                    //AddParam(sqlCmd, OutboxTableConfig.CreatedDateTimeUtcFieldName, outboxItem.CreatedDateTimeUtc, batchIndex);
//                    AddParam(sqlCmd, OutboxTableConfig.StatusFieldName, outboxItem.Status.ToString(), batchIndex);
//                    AddParam(sqlCmd, OutboxTableConfig.PublishAttemptsFieldName, outboxItem.PublishAttempts, batchIndex);
//                    AddParam(sqlCmd, OutboxTableConfig.PublishTargetFieldName, outboxItem.PublishTarget, batchIndex);
//                    AddParam(sqlCmd, OutboxTableConfig.PayloadFieldName, outboxItem.Payload, batchIndex);
//                }

//                //Execute the Batch and continue...
//                await using var sqlReader = await sqlCmd.ExecuteReaderAsync().ConfigureAwait(false);

//                //Since some fields are actually populated in the Database, we post-process to update the models with valid
//                //  values as returned from teh Insert process...
//                var outboxBatchLookup = batch.ToLookup(i => i.UniqueIdentifier);
//                while (await sqlReader.ReadAsync().ConfigureAwait(false))
//                {
//                    //The First field is always our UniqueIdentifier (as defined by the Output clause of the Sql)
//                    // and the Second field is always the UTC Created DateTime returned from the Database.
//                    var uniqueIdentifier = ConvertUniqueIdentifierFromDb(sqlReader);
//                    var createdDateUtcFromDb = (DateTime)sqlReader[OutboxTableConfig.CreatedDateTimeUtcFieldName];

//                    var outboxItem = outboxBatchLookup[uniqueIdentifier].First();
//                    outboxItem.CreatedDateTimeUtc = createdDateUtcFromDb;
//                }
//            }

//            return outboxItemsList;
//        }

//        public override async Task<List<ISqlTransactionalOutboxItem<Guid>>> UpdateOutboxItemsAsync(
//            IEnumerable<ISqlTransactionalOutboxItem<Guid>> outboxItems,
//            int updateBatchSize = 20
//        )
//        {
//            await using var sqlCmd = CreateSqlCommand("");

//            var outboxItemsList = outboxItems.ToList();

//            var batches = outboxItemsList.Chunk(updateBatchSize);
//            foreach (var batch in batches)
//            {
//                sqlCmd.CommandText = QueryBuilder.BuildParameterizedSqlToUpdateExistingOutboxItem(batch);
//                sqlCmd.Parameters.Clear();

//                //Add the Parameters!
//                for (var batchIndex = 0; batchIndex < batch.Length; batchIndex++)
//                {
//                    var outboxItem = batch[batchIndex];

//                    //Unique Identifier is used for Identification Match!
//                    var uniqueIdentifierForDb = ConvertUniqueIdentifierForDb(outboxItem.UniqueIdentifier);
//                    AddParam(sqlCmd, OutboxTableConfig.UniqueIdentifierFieldName, uniqueIdentifierForDb, batchIndex);

//                    //NOTE: The only Updateable Fields are Status & PublishAttempts
//                    AddParam(sqlCmd, OutboxTableConfig.StatusFieldName, outboxItem.Status.ToString(), batchIndex);
//                    AddParam(sqlCmd, OutboxTableConfig.PublishAttemptsFieldName, outboxItem.PublishAttempts, batchIndex);
//                }

//                //Execute the Batch and continue...
//                await sqlCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
//            }

//            return outboxItemsList;
//        }
//    }
//}
