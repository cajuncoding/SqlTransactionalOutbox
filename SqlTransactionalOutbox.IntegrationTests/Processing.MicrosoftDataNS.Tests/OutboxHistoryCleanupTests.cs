using System;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlTransactionalOutbox.SqlServer.MicrosoftDataNS;
using SqlTransactionalOutbox.Tests;

namespace SqlTransactionalOutbox.IntegrationTests.MicrosoftDataNS
{
    [TestClass]
    public class OutboxHistoryCleanupTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task TestOutboxHistoryCleanupTests()
        {
            //Clear the Table data for the test...
            int testDataSize = 100;
            await MicrosoftDataSqlTestHelpers.PopulateTransactionalOutboxTestDataAsync(testDataSize, true);

            var sqlConnection = await SqlConnectionHelper.CreateMicrosoftDataSqlConnectionAsync();

            var insertedCount = await RetrievePendingItemsCountAsync(sqlConnection);
            Assert.AreEqual(testDataSize, insertedCount);

            await Task.Delay(TimeSpan.FromSeconds(5));

            int nonPurgedDataSize = 10;
            await MicrosoftDataSqlTestHelpers.PopulateTransactionalOutboxTestDataAsync(nonPurgedDataSize, false);

            //Execute
            await sqlConnection
                .CleanupHistoricalOutboxItemsAsync(TimeSpan.FromSeconds(3))
                .ConfigureAwait(false);

            //Assert
            var purgedCount = await RetrievePendingItemsCountAsync(sqlConnection);
            Assert.AreEqual(nonPurgedDataSize, purgedCount);
        }

        private async Task<int> RetrievePendingItemsCountAsync(SqlConnection sqlConnection)
        {
            var sqlTransaction = (SqlTransaction)await sqlConnection.BeginTransactionAsync().ConfigureAwait(false);
            var sqlOutboxRepository = new DefaultSqlServerOutboxRepository<string>(sqlTransaction);

            var pendingItems = await sqlOutboxRepository
                .RetrieveOutboxItemsAsync(OutboxItemStatus.Pending)
                .ConfigureAwait(false);
            
            await sqlTransaction.RollbackAsync();
            return pendingItems.Count;
        }
    }
}
