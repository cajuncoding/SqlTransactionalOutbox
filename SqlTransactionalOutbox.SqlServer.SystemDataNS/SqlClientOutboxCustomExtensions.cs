using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.Utilities;

namespace SqlTransactionalOutbox.SqlServer.SystemDataNS
{
    public static class SqlClientOutboxCustomExtensions
    {
        /// <summary>
        /// Convenience method for adding an item easily from a JSON representation of data to add to the Outbox.  Using
        /// this approach is highly flexible as the payload can control the Outbox and publishing process in an non-coupled
        /// way (via late binding).  Validation of required fields will result in exceptions.
        /// This will parse and process the json payload with the Transactional Outbox using Default implementations (e.g. GUID identifier).
        /// This method will create and commit the Transaction automatically, but may error if a running transaction
        /// is already in progress in which case the custom extension of the Transaction should be used directly instead.
        /// </summary>
        /// <param name="sqlConnection"></param>
        /// <param name="jsonText"></param>
        /// <param name="publishTopic"></param>
        /// <param name="fifoGroupingIdentifier"></param>
        /// <returns></returns>
        public static async Task<ISqlTransactionalOutboxItem<Guid>> AddTransactionalOutboxPendingItemAsync(
            this SqlConnection sqlConnection,
            string jsonText,
            string publishTopic = null,
            string fifoGroupingIdentifier = null
        )
        {
            var payloadBuilder = PayloadBuilder.FromJsonSafely(jsonText);

            //Publishing Target may be defined in the Payload OR as a discrete parameter that overrides the payload, 
            //  but it is REQUIRED!
            var publishingTarget = publishTopic ?? payloadBuilder.PublishTarget;
            publishingTarget.AssertNotNullOrWhiteSpace(nameof(payloadBuilder.PublishTarget), "No Publishing Topic was defined in the Payload or as a parameter.");

            //FIFO Grouping Identifier may be defined in the Payload OR as a discrete parameter that overrides the payload,
            //  but it is OPTIONAL.
            var fifoGroupId = fifoGroupingIdentifier ?? payloadBuilder.FifoGroupingId;

            var results = await sqlConnection
                .AddTransactionalOutboxPendingItemAsync(
                    publishingTarget,
                    payloadBuilder.ToJObject(),
                    fifoGroupId
                )
                .ConfigureAwait(false);

            return results;
        }

        /// <summary>
        /// Convenience method for adding an item easily to the Transactional Outbox using Default implementations (e.g. GUID identifier).
        /// This method will create and commit the Transaction automatically, but may error if a running transaction
        /// is already in progress in which case the custom extension of the Transaction should be used directly instead.
        /// </summary>
        /// <typeparam name="TPayload"></typeparam>
        /// <param name="sqlConnection"></param>
        /// <param name="publishTarget"></param>
        /// <param name="payload"></param>
        /// <param name="fifoGroupingIdentifier"></param>
        /// <returns></returns>
        public static async Task<ISqlTransactionalOutboxItem<Guid>> AddTransactionalOutboxPendingItemAsync<TPayload>(
            this SqlConnection sqlConnection,
            string publishTarget,
            TPayload payload,
            string fifoGroupingIdentifier = null
        )
        {
            sqlConnection.AssertSqlConnectionIsValid();
            await using var outboxTransaction = (SqlTransaction)(await sqlConnection.BeginTransactionAsync());

            try
            {
                var results = await outboxTransaction
                    .AddTransactionalOutboxPendingItemAsync(publishTarget, payload, fifoGroupingIdentifier)
                    .ConfigureAwait(false);

                await outboxTransaction.CommitAsync().ConfigureAwait(false);

                return results;

            }
            catch (Exception)
            {
                await outboxTransaction.RollbackAsync().ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>
        /// Convenience method for adding an item easily to the Transactional Outbox using Default implementations (e.g. GUID identifier).
        /// This method will create and commit the Transaction automatically, but may error if a running transaction
        /// is already in progress in which case the custom extension of the Transaction should be used directly instead.
        /// </summary>
        /// <typeparam name="TPayload"></typeparam>
        /// <param name="sqlConnection"></param>
        /// <param name="outboxInsertionItems"></param>
        /// <returns></returns>
        public static async Task<List<ISqlTransactionalOutboxItem<Guid>>> AddTransactionalOutboxPendingItemListAsync<TPayload>(
            this SqlConnection sqlConnection,
            IEnumerable<ISqlTransactionalOutboxInsertionItem<TPayload>> outboxInsertionItems
        )
        {
            sqlConnection.AssertSqlConnectionIsValid();
            await using var outboxTransaction = (SqlTransaction)(await sqlConnection.BeginTransactionAsync());

            try
            {
                var results = await outboxTransaction
                    .AddTransactionalOutboxPendingItemListAsync(outboxInsertionItems)
                    .ConfigureAwait(false);

                await outboxTransaction.CommitAsync().ConfigureAwait(false);

                return results;
            }
            catch (Exception)
            {
                await outboxTransaction.RollbackAsync().ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>
        /// Execute the cleanup (e.g. Purge) of Historical data from the Outbox for the specified timespan
        /// of how much time to keep (e.g. last 30 days, last 90 days, etc.).
        /// This method will create and commit the Transaction automatically, but may error if a running transaction
        /// is already in progress in which case the custom extension of the Transaction should be used directly instead.
        /// </summary>
        /// <param name="sqlConnection"></param>
        /// <param name="historyTimeToKeepTimeSpan"></param>
        /// <returns></returns>
        public static async Task CleanupHistoricalOutboxItemsAsync(
            this SqlConnection sqlConnection,
            TimeSpan historyTimeToKeepTimeSpan
        )
        {
            sqlConnection.AssertSqlConnectionIsValid();
            await using var outboxTransaction = (SqlTransaction)(await sqlConnection.BeginTransactionAsync());

            try
            {
                await outboxTransaction
                    .CleanupHistoricalOutboxItemsAsync(historyTimeToKeepTimeSpan)
                    .ConfigureAwait(false);

                await outboxTransaction.CommitAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                await outboxTransaction.RollbackAsync().ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>
        /// Convenience method for adding an item easily from a JSON representation of data to add to the Outbox.  Using
        /// this approach is highly flexible as the payload can control the Outbox and publishing process in an non-coupled
        /// way (via late binding).  Validation of required fields will result in exceptions.
        /// This will parse and process the json payload with the Transactional Outbox using Default implementations (e.g. GUID identifier).
        /// This method assumes the current Transaction and associated Connection is valid and will use it but will not commit the Transaction!
        /// </summary>
        /// <param name="sqlTransaction"></param>
        /// <param name="jsonText"></param>
        /// <param name="publishTopic"></param>
        /// <param name="fifoGroupingIdentifier"></param>
        /// <returns></returns>
        public static async Task<ISqlTransactionalOutboxItem<Guid>> AddTransactionalOutboxPendingItemAsync(
            this SqlTransaction sqlTransaction,
            string jsonText,
            string publishTopic = null,
            string fifoGroupingIdentifier = null
        )
        {
            var payloadBuilder = PayloadBuilder.FromJsonSafely(jsonText);

            //Publishing Target may be defined in the Payload OR as a discrete parameter that overrides the payload, 
            //  but it is REQUIRED!
            var publishingTarget = publishTopic ?? payloadBuilder.PublishTarget;
            publishingTarget.AssertNotNullOrWhiteSpace(nameof(payloadBuilder.PublishTarget), "No Publishing Topic was defined in the Payload or as a parameter.");

            //FIFO Grouping Identifier may be defined in the Payload OR as a discrete parameter that overrides the payload,
            //  but it is OPTIONAL.
            var fifoGroupId = fifoGroupingIdentifier ?? payloadBuilder.FifoGroupingId;

            var results = await sqlTransaction
                .AddTransactionalOutboxPendingItemAsync(
                    publishingTarget,
                    payloadBuilder.ToJObject(),
                    fifoGroupId
                )
                .ConfigureAwait(false);

            return results;
        }

        /// <summary>
        /// Convenience method for adding an item easily to the Transactional Outbox using Default implementations (e.g. GUID identifier).
        /// This method assumes the current Transaction and associated Connection is valid and will use it but will not commit the Transaction!
        /// </summary>
        /// <typeparam name="TPayload"></typeparam>
        /// <param name="sqlTransaction"></param>
        /// <param name="publishTarget"></param>
        /// <param name="jsonPayload"></param>
        /// <param name="fifoGroupingIdentifier"></param>
        /// <returns></returns>
        public static async Task<ISqlTransactionalOutboxItem<Guid>> AddTransactionalOutboxPendingItemAsync<TPayload>(
            this SqlTransaction sqlTransaction,
            string publishTarget,
            TPayload jsonPayload,
            string fifoGroupingIdentifier = null
        )
        {
            sqlTransaction.AssertSqlTransactionIsValid();

            //SAVE the Item to the Outbox...
            var outbox = new DefaultSqlServerTransactionalOutbox<TPayload>(sqlTransaction);
            var outboxItem = await outbox.InsertNewPendingOutboxItemAsync(
                publishingTarget: publishTarget,
                publishingPayload: jsonPayload,
                fifoGroupingIdentifier: fifoGroupingIdentifier
            ).ConfigureAwait(false);

            return outboxItem;
        }

        /// <summary>
        /// Convenience method for adding an item easily to the Transactional Outbox using Default implementations (e.g. GUID identifier).
        /// This method assumes the current Transaction and associated Connection is valid and will use it but will not commit the Transaction!
        /// </summary>
        /// <typeparam name="TPayload"></typeparam>
        /// <param name="sqlTransaction"></param>
        /// <param name="outboxInsertionItems"></param>
        /// <returns></returns>
        public static async Task<List<ISqlTransactionalOutboxItem<Guid>>> AddTransactionalOutboxPendingItemListAsync<TPayload>(
            this SqlTransaction sqlTransaction,
            IEnumerable<ISqlTransactionalOutboxInsertionItem<TPayload>> outboxInsertionItems
        )
        {
            sqlTransaction.AssertSqlTransactionIsValid();

            //SAVE the Item to the Outbox...
            var outbox = new DefaultSqlServerTransactionalOutbox<TPayload>(sqlTransaction);
            var outboxItems = await outbox
                .InsertNewPendingOutboxItemsAsync(outboxInsertionItems)
                .ConfigureAwait(false);

            return outboxItems;
        }

        /// <summary>
        /// Execute the cleanup (e.g. Purge) of Historical data from the Outbox for the specified timespan
        /// of how much time to keep (e.g. last 30 days, last 90 days, etc.).
        /// </summary>
        /// <param name="sqlTransaction"></param>
        /// <param name="historyTimeToKeepTimeSpan"></param>
        /// <returns></returns>
        public static async Task CleanupHistoricalOutboxItemsAsync(
            this SqlTransaction sqlTransaction,
            TimeSpan historyTimeToKeepTimeSpan
        )
        {
            sqlTransaction.AssertSqlTransactionIsValid();

            //NOTE: Payload Type isn't critical when executing the Cleanup process, and all payloads are stored in
            //      serialized form so to simplify the custom extension signature we can just use string payload type here.
            var outbox = new DefaultSqlServerTransactionalOutbox<string>(sqlTransaction);
            await outbox
                .CleanupHistoricalOutboxItemsAsync(historyTimeToKeepTimeSpan)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Validate that the Sql Connection is not null, and connection is open, etc.
        /// </summary>
        /// <param name="sqlConnection"></param>
        public static void AssertSqlConnectionIsValid(this SqlConnection sqlConnection)
        {
            sqlConnection.AssertNotNull(nameof(sqlConnection));
            if (sqlConnection.State != ConnectionState.Open)
                throw new Exception("Sql Connection provided is not yet open.");

        }

        /// <summary>
        /// Validate that the Sql Transaction is not null, and the associated connection is valid/open, etc.
        /// </summary>
        /// <param name="sqlTransaction"></param>
        public static void AssertSqlTransactionIsValid(this SqlTransaction sqlTransaction)
        {
            sqlTransaction.AssertNotNull(nameof(sqlTransaction));
            if (sqlTransaction?.Connection.State != ConnectionState.Open)
                throw new Exception("Sql Connection for the provided Sql Transaction is not open.");
        }
    }
}
