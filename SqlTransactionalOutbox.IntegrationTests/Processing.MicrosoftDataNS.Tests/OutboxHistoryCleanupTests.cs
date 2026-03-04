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
            TimeSpan deferredScheduleTime = TimeSpan.FromSeconds(20);

            //Load immeidate delivery outbox items... should be purged by cleanup method!
            await MicrosoftDataSqlTestHelpers.PopulateTransactionalOutboxTestDataAsync(testDataSize, true);

            var sqlConnection = await SqlConnectionHelper.CreateMicrosoftDataSqlConnectionAsync();

            var insertedCount = await RetrievePendingItemsCountAsync(sqlConnection);
            Assert.AreEqual(testDataSize, insertedCount);

            await Task.Delay(TimeSpan.FromSeconds(5));

            //Load future/scheduled delivery outbox items... should NOT be purged by cleanup method as they are intentionally pending future Delivery!
            int nonPurgedDataSize = 10;
            await MicrosoftDataSqlTestHelpers.PopulateTransactionalOutboxTestDataAsync(
                nonPurgedDataSize,
                clearExistingOutbox: false,
                scheduledPublishDateTime: DateTimeOffset.UtcNow.Add(deferredScheduleTime));

            //Execute
            await sqlConnection
                .CleanupHistoricalOutboxItemsAsync(historyTimeToKeepTimeSpan: TimeSpan.Zero)
                .ConfigureAwait(false);

            //Assert
            //NOTE: To get pending items without any Scheduled Items we use the default (TimeSpan.Zero) for Pending Prefetch Time!
            var newPurgedCountExcludingScheduledItems = await RetrievePendingItemsCountAsync(sqlConnection);
            Assert.AreEqual(0, newPurgedCountExcludingScheduledItems);

            //NOTE: To get pending items including any Scheduled Items we can use the prefetch feature to specify the Prefetch Time
            //  which will look ahead and retrieve items Scheduled within that amount of time; rather than physically waiting here for time to pass
            //  the schduled target!
            var newPurgedCountIncludingScheduledItems = await RetrievePendingItemsCountAsync(sqlConnection, deferredScheduleTime * 2);
            Assert.AreEqual(nonPurgedDataSize, newPurgedCountIncludingScheduledItems);
        }

        private async Task<int> RetrievePendingItemsCountAsync(SqlConnection sqlConnection, TimeSpan? scheduledPublishPrefetchTime = null)
        {
            var sqlTransaction = (SqlTransaction)await sqlConnection.BeginTransactionAsync().ConfigureAwait(false);
            var sqlOutboxRepository = new DefaultSqlServerOutboxRepository<string>(sqlTransaction);

            var pendingItems = await sqlOutboxRepository
                .RetrieveOutboxItemsAsync(OutboxItemStatus.Pending, scheduledPublishPrefetchTime: scheduledPublishPrefetchTime)
                .ConfigureAwait(false);
            
            await sqlTransaction.RollbackAsync();
            return pendingItems.Count;
        }
    }
}
