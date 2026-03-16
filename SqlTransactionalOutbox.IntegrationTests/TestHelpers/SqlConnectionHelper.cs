using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SqlTransactionalOutbox.SqlServer.MicrosoftDataNS;
using Microsoft.Data.SqlClient;

namespace SqlTransactionalOutbox.Tests
{
    public class SqlConnectionHelper
    {
        public static async Task<SqlConnection> CreateMicrosoftDataSqlConnectionAsync()
        {
            var sqlConnection = new SqlConnection(TestConfiguration.SqlConnectionString);
            await sqlConnection.OpenAsync();
            return sqlConnection;
        }
    }

    public static class SqlCommands
    {
        public static readonly string TruncateTransactionalOutbox =
            $"TRUNCATE TABLE [{OutboxTableConfig.DefaultTransactionalOutboxSchemaName}].[{OutboxTableConfig.DefaultTransactionalOutboxTableName}]";

    }

    public static class SqlConnectionCustomExtensionsForMicrosoftData
    {
        public static async Task TruncateTransactionalOutboxTableAsync(this SqlConnection sqlConnection)
        {
            //CLEAR the Table for Integration Tests to validate:
            await using var sqlCmd = new SqlCommand(
                SqlCommands.TruncateTransactionalOutbox,
                sqlConnection
            );
            await sqlCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    public static class MicrosoftDataSqlTestHelpers
    {
        public static async Task<List<ISqlTransactionalOutboxItem<Guid>>> PopulateTransactionalOutboxTestDataAsync(
            int testDataSize,
            bool clearExistingOutbox = true,
            DateTimeOffset? scheduledPublishDateTime = null
        )
        {
            //Organize
            await using var sqlConnection = await SqlConnectionHelper.CreateMicrosoftDataSqlConnectionAsync();

            //*****************************************************************************************
            //* STEP 1 - Prepare/Clear the Queue Table
            //*****************************************************************************************
            //Clear the Table data for the test...
            if (clearExistingOutbox)
                await sqlConnection.TruncateTransactionalOutboxTableAsync();

            //*****************************************************************************************
            //* STEP 2 - Insert New Outbox Items to process with TestHarness for Publishing...
            //*****************************************************************************************
            var outboxTestItems = TestHelper.CreateTestStringOutboxItemData(testDataSize, scheduledPublishDateTime: scheduledPublishDateTime);
            var insertedResults = await sqlConnection
                .AddTransactionalOutboxPendingItemListAsync(outboxTestItems)
                .ConfigureAwait(false);

            return insertedResults;
        }
    }
}
