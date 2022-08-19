using System;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox.SqlServer.MicrosoftDataNS
{
    public static class SqlClientOutboxProcessingCustomExtensions
    {
        public static async Task<ISqlTransactionalOutboxProcessingResults<Guid>> ProcessPendingOutboxItemsAsync(
            this SqlConnection sqlConnection,
            ISqlTransactionalOutboxPublisher<Guid> outboxPublisher,
            OutboxProcessingOptions processingOptions,
            bool throwExceptionOnFailure = false
        )
        {
            sqlConnection.AssertSqlConnectionIsValid();
            outboxPublisher.AssertNotNull(nameof(outboxPublisher));
            processingOptions.AssertNotNull(nameof(processingOptions));

            await using var outboxTransaction = (SqlTransaction)(await sqlConnection.BeginTransactionAsync().ConfigureAwait(false));
            try
            {
                var results = await outboxTransaction
                    .ProcessPendingOutboxItemsAsync(
                        outboxPublisher: outboxPublisher,
                        processingOptions: processingOptions,
                        throwExceptionOnFailure: throwExceptionOnFailure
                    )
                    .ConfigureAwait(false);

                await outboxTransaction.CommitAsync().ConfigureAwait(false);

                return results;
            }
            catch (Exception exc)
            {
                //FIRST Rollback any pending changes...
                await outboxTransaction.RollbackAsync().ConfigureAwait(false);

                try
                {
                    //THEN Attempt any Mitigating Actions for the Issue...
                    //IF WE have issues retrieving the new items from the DB then we attempt to increment the
                    //  Publish Attempts in case there is an issue with the entry that is causing failures, so
                    //  that any potential problematic items will eventually be failed out and skipped.
                    await sqlConnection.IncrementPublishAttemptsForAllPendingItemsAsync(outboxPublisher).ConfigureAwait(false);
                }
                catch (Exception mitigationExc)
                {
                    throw new AggregateException(new[] { exc, mitigationExc });
                }

                //FINALLY Re-throw to ensure we don't black hole the issue...
                throw;
            }
        }

        public static async Task<ISqlTransactionalOutboxProcessingResults<Guid>> ProcessPendingOutboxItemsAsync(
            this SqlTransaction sqlTransaction,
            ISqlTransactionalOutboxPublisher<Guid> outboxPublisher,
            OutboxProcessingOptions processingOptions,
            bool throwExceptionOnFailure = false
        )
        {
            sqlTransaction.AssertSqlTransactionIsValid();
            outboxPublisher.AssertNotNull(nameof(outboxPublisher));
            processingOptions.AssertNotNull(nameof(processingOptions));

            //NOTE: Payload type isn't important when Publishing because we publish the already serialized
            //      payload anyway so to simplify the custom extension signature we can just use string payload type here.
            var outboxProcessor = new DefaultSqlServerTransactionalOutboxProcessor<string>(sqlTransaction, outboxPublisher);

            var results = await outboxProcessor
                .ProcessPendingOutboxItemsAsync(processingOptions, throwExceptionOnFailure)
                .ConfigureAwait(false);

            return results;
        }

        private static async Task IncrementPublishAttemptsForAllPendingItemsAsync(
            this SqlConnection sqlConnection,
            ISqlTransactionalOutboxPublisher<Guid> outboxPublisher
        )
        {
            outboxPublisher.AssertNotNull(nameof(outboxPublisher));

            await using var outboxTransaction = (SqlTransaction)(await sqlConnection.BeginTransactionAsync().ConfigureAwait(false));

            var outboxProcessor = new DefaultSqlServerTransactionalOutboxProcessor<string>(outboxTransaction, outboxPublisher);
            var outboxRepository = outboxProcessor.OutboxRepository;
            
            await outboxRepository
                .IncrementPublishAttemptsForAllItemsByStatusAsync(OutboxItemStatus.Pending)
                .ConfigureAwait(false);

            await outboxTransaction.CommitAsync();
        }

    }
}
