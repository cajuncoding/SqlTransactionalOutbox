using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SqlTransactionalOutbox.SqlServer.SystemDataNS;
using SystemData = System.Data.SqlClient;
using MicrosoftData = Microsoft.Data.SqlClient;

namespace SqlTransactionalOutbox.Tests
{
    public class SqlConnectionHelper
    {
        public static async Task<SystemData.SqlConnection> CreateSystemDataSqlConnectionAsync()
        {
            var sqlConnection = new SystemData.SqlConnection(TestConfiguration.SqlConnectionString);
            await sqlConnection.OpenAsync();
            return sqlConnection;
        }

        public static async Task<MicrosoftData.SqlConnection> CreateMicrosoftDataSqlConnectionAsync()
        {
            var sqlConnection = new MicrosoftData.SqlConnection(TestConfiguration.SqlConnectionString);
            await sqlConnection.OpenAsync();
            return sqlConnection;
        }
    }

    public static class SqlCommands
    {
        public static readonly string TruncateTransactionalOutbox =
            $"TRUNCATE TABLE [{OutboxTableConfig.DefaultTransactionalOutboxSchemaName}].[{OutboxTableConfig.DefaultTransactionalOutboxTableName}]";

    }

    public static class SqlConnectionCustomExtensionsForSystemData
    {
        public static async Task TruncateTransactionalOutboxTableAsync(this SystemData.SqlConnection sqlConnection)
        {
            //CLEAR the Table for Integration Tests to validate:
            await using var sqlCmd = new SystemData.SqlCommand(
                SqlCommands.TruncateTransactionalOutbox,
                sqlConnection
            );
            await sqlCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    public static class SqlConnectionCustomExtensionsForMicrosoftData
    {
        public static async Task TruncateTransactionalOutboxTableAsync(this MicrosoftData.SqlConnection sqlConnection)
        {
            //CLEAR the Table for Integration Tests to validate:
            await using var sqlCmd = new MicrosoftData.SqlCommand(
                SqlCommands.TruncateTransactionalOutbox,
                sqlConnection
            );
            await sqlCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    public static class SystemDataSqlTestHelpers
    {
        public static async Task<List<ISqlTransactionalOutboxItem<Guid>>> PopulateTransactionalOutboxTestDataAsync(int testDataSize, bool clearExistingOutbox = true)
        {
            //Organize
            await using var sqlConnection = await SqlConnectionHelper.CreateSystemDataSqlConnectionAsync();

            //*****************************************************************************************
            //* STEP 1 - Prepare/Clear the Queue Table
            //*****************************************************************************************
            //Clear the Table data for the test...
            if(clearExistingOutbox)
                await sqlConnection.TruncateTransactionalOutboxTableAsync();

            //*****************************************************************************************
            //* STEP 2 - Insert New Outbox Items to process with TestHarness for Publishing...
            //*****************************************************************************************
            var outboxTestItems = TestHelper.CreateTestStringOutboxItemData(testDataSize);
            var insertedResults = await sqlConnection
                .AddTransactionalOutboxPendingItemListAsync(outboxTestItems)
                .ConfigureAwait(false);

            return insertedResults;
        }
    }
}
