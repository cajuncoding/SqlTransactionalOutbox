using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox.SqlServer.SystemDataNS
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
            catch (Exception)
            {
                await outboxTransaction.RollbackAsync().ConfigureAwait(false);
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



    }
}
