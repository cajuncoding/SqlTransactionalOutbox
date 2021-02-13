using System;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.SqlServer.MicrosoftDataNS;
using SqlTransactionalOutbox.Tests;

namespace SqlTransactionalOutbox.IntegrationTests.MicrosoftDataNS
{
    [TestClass]
    public class OutboxItemInsertionTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task TestNewOutboxItemInsertionBenchmark()
        {
            //Organize
            await using var sqlConnection = await SqlConnectionHelper.CreateMicrosoftDataSqlConnectionAsync();

            //WARM UP...
            var warmupTestItems = TestHelper.CreateTestStringOutboxItemData(2);

            var warmupTimer = Stopwatch.StartNew();
            var warmupResults = await sqlConnection
                .AddTransactionalOutboxPendingItemListAsync(warmupTestItems)
                .ConfigureAwait(false);
            warmupTimer.Stop();

            TestContext?.WriteLine($"Warm-up Inserted [{warmupResults.Count}] items in [{warmupTimer.Elapsed.ToElapsedTimeDescriptiveFormat()}].");

            //Execute... Full Test...
            var executionTestItems = TestHelper.CreateTestStringOutboxItemData(1000);

            var timer = Stopwatch.StartNew();
            var executionResults = await sqlConnection
                .AddTransactionalOutboxPendingItemListAsync(warmupTestItems)
                .ConfigureAwait(false);
            timer.Stop();

            TestContext?.WriteLine($"Benchmark Execution Inserted [{executionResults.Count}] items in [{timer.Elapsed.ToElapsedTimeDescriptiveFormat()}].");
        }

        [TestMethod]
        public async Task TestNewOutboxItemInsertionByBatch()
        {
            //Organize
            await using var sqlConnection = await SqlConnectionHelper.CreateMicrosoftDataSqlConnectionAsync();

            //Clear the Table data for the test...
            await sqlConnection.TruncateTransactionalOutboxTableAsync();

            //Initialize Transaction and Outbox Processor
            var outboxTestItems = TestHelper.CreateTestStringOutboxItemData(100);

            //Execute
            var timer = Stopwatch.StartNew();

            var insertedResults = await sqlConnection
                .AddTransactionalOutboxPendingItemListAsync(outboxTestItems)
                .ConfigureAwait(false);

            timer.Stop();
            TestContext?.WriteLine($"Inserted [{insertedResults.Count}] items in [{timer.Elapsed.ToElapsedTimeDescriptiveFormat()}].");

            //Assert
            Assert.AreEqual(insertedResults.Count, outboxTestItems.Count);
            insertedResults.ForEach(i =>
            {
                //Validate Created Date Time (can't match precisely but can validate it was populated as expected...
                Assert.AreEqual(OutboxItemStatus.Pending, i.Status);
                Assert.AreNotEqual(DateTimeOffset.MinValue, i.CreatedDateTimeUtc);
                Assert.AreEqual(0, i.PublishAttempts);
                Assert.IsFalse(string.IsNullOrWhiteSpace(i.PublishTarget));
                Assert.IsFalse(string.IsNullOrWhiteSpace(i.Payload));
                Assert.AreNotEqual(Guid.Empty, i.UniqueIdentifier);
            });
        }


        [TestMethod]
        public async Task TestNewOutboxItemInsertionIndividualItemsViaConvenienceMethods()
        {
            //Organize
            await using var sqlConnection = await SqlConnectionHelper.CreateMicrosoftDataSqlConnectionAsync();

            //Clear the Table data for the test...
            await sqlConnection.TruncateTransactionalOutboxTableAsync();

            //Initialize Transaction and Outbox Processor
            var outboxTestItems = TestHelper.CreateTestStringOutboxItemData(2);

            //Execute
            var timer = Stopwatch.StartNew();

            //Add Item 1!
            var insertedResult1 = await sqlConnection
                .AddTransactionalOutboxPendingItemAsync(
                    outboxTestItems[0].PublishingTarget,
                    outboxTestItems[0].PublishingPayload,
                    outboxTestItems[0].FifoGroupingIdentifier
                )
                .ConfigureAwait(false);

            TestContext?.WriteLine($"Inserted First Item in [{timer.Elapsed.ToElapsedTimeDescriptiveFormat()}].");
            Assert.IsNotNull(insertedResult1);
            Assert.IsNotNull(insertedResult1.UniqueIdentifier);
            Assert.AreEqual(DateTime.UtcNow.Date, insertedResult1.CreatedDateTimeUtc.Date);

            //Add Item 2 (No FifoGroupingIdentifier)!
            var insertedResult2 = await sqlConnection
                .AddTransactionalOutboxPendingItemAsync(
                    outboxTestItems[1].PublishingTarget,
                    outboxTestItems[1].PublishingPayload
                )
                .ConfigureAwait(false);

            TestContext?.WriteLine($"Inserted First Item in [{timer.Elapsed.ToElapsedTimeDescriptiveFormat()}].");
            Assert.IsNotNull(insertedResult2);
            Assert.IsNotNull(insertedResult2.UniqueIdentifier);
            Assert.AreEqual(DateTime.UtcNow.Date, insertedResult2.CreatedDateTimeUtc.Date);

            timer.Stop();
        }

        [TestMethod]
        public async Task TestNewOutboxItemInsertionAndRetrieval()
        {
            var testPublisher = new TestHarnessSqlTransactionalOutboxPublisher();

            //Create Test Data
            var insertedResults = await MicrosoftDataSqlTestHelpers.PopulateTransactionalOutboxTestDataAsync(100);

            //Initialize Transaction and Outbox Processor
            await using var sqlConnection = await SqlConnectionHelper.CreateMicrosoftDataSqlConnectionAsync().ConfigureAwait(false);
            await using var sqlTransaction = (SqlTransaction)await sqlConnection.BeginTransactionAsync().ConfigureAwait(false);

            var outboxProcessor = new DefaultSqlServerTransactionalOutboxProcessor<string>(sqlTransaction, testPublisher);

            //RETRIEVE ALL Pending Items from the Outbox to validate
            var pendingResults = await outboxProcessor.OutboxRepository
                .RetrieveOutboxItemsAsync(OutboxItemStatus.Pending)
                .ConfigureAwait(false);

            //Assert
            TestHelper.AssertOutboxItemResultsMatch(insertedResults, pendingResults);
        }
    }
}
