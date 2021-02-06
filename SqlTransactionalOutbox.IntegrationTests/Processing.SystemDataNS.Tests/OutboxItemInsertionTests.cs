using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.SqlServer.SystemDataNS;
using SqlTransactionalOutbox.Tests;

namespace SqlTransactionalOutbox.IntegrationTests
{
    [TestClass]
    public class OutboxItemInsertionTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task TestNewOutboxItemInsertionBenchmark()
        {
            //Organize
            await using var sqlConnection = await SqlConnectionHelper.CreateSystemDataSqlConnectionAsync();

            //WARM UP...
            var warmupTestItems = TestHelper.CreateTestStringOutboxItemData(2);
            await using var warmupTransaction = (SqlTransaction)await sqlConnection.BeginTransactionAsync().ConfigureAwait(false);
            var outbox = new DefaultSqlServerTransactionalOutbox<string>(warmupTransaction);

            var warmupTimer = Stopwatch.StartNew();
            var warmupResults = await outbox.InsertNewPendingOutboxItemsAsync(warmupTestItems).ConfigureAwait(false);
            await warmupTransaction.CommitAsync();
            warmupTimer.Stop();

            TestContext?.WriteLine($"Warm-up Inserted [{warmupResults.Count}] items in [{warmupTimer.Elapsed.ToElapsedTimeDescriptiveFormat()}].");

            //Execute... Full Test...
            var executionTestItems = TestHelper.CreateTestStringOutboxItemData(1000);
            await using var sqlTransaction2 = (SqlTransaction)await sqlConnection.BeginTransactionAsync().ConfigureAwait(false);
            outbox = new DefaultSqlServerTransactionalOutbox<string>(sqlTransaction2);

            var timer = Stopwatch.StartNew();
            var executionResults = await outbox.InsertNewPendingOutboxItemsAsync(executionTestItems).ConfigureAwait(false);
            await sqlTransaction2.CommitAsync();
            timer.Stop();
            
            TestContext?.WriteLine($"Benchmark Execution Inserted [{executionResults.Count}] items in [{timer.Elapsed.ToElapsedTimeDescriptiveFormat()}].");
        }

        [TestMethod]
        public async Task TestNewOutboxItemInsertionByBatch()
        {
            //Organize
            await using var sqlConnection = await SqlConnectionHelper.CreateSystemDataSqlConnectionAsync();

            //Clear the Table data for the test...
            await sqlConnection.TruncateTransactionalOutboxTableAsync();

            //Initialize Transaction and Outbox Processor
            await using var sqlTransaction = (SqlTransaction)await sqlConnection.BeginTransactionAsync().ConfigureAwait(false);

            var outbox = new DefaultSqlServerTransactionalOutbox<string>(sqlTransaction);

            var outboxTestItems = TestHelper.CreateTestStringOutboxItemData(100);

            //Execute
            var timer = Stopwatch.StartNew();
            
            var insertedResults = await outbox
                .InsertNewPendingOutboxItemsAsync(outboxTestItems)
                .ConfigureAwait(false);

            await sqlTransaction.CommitAsync();

            timer.Stop();
            TestContext?.WriteLine($"Inserted [{insertedResults.Count}] items in [{timer.Elapsed.ToElapsedTimeDescriptiveFormat()}].");

            //Assert
            Assert.AreEqual(insertedResults.Count, outboxTestItems.Count);
            insertedResults.ForEach(i =>
            {
                //Validate Created Date Time (can't match precisely but can validate it was populated as expected...
                Assert.AreEqual(OutboxItemStatus.Pending, i.Status);
                Assert.AreNotEqual(DateTime.MinValue, i.CreatedDateTimeUtc);
                Assert.AreEqual(0, i.PublishAttempts);
                Assert.IsFalse(string.IsNullOrWhiteSpace(i.PublishTarget));
                Assert.IsFalse(string.IsNullOrWhiteSpace(i.Payload));
                Assert.AreNotEqual(Guid.Empty, i.UniqueIdentifier);
            });
        }

        [TestMethod]
        public async Task TestNewOutboxItemInsertionAndRetrieval()
        {
            var testPublisher = new TestHarnessSqlTransactionalOutboxPublisher();

            //Create Test Data
            var insertedResults = await SystemDataSqlTestHelpers.PopulateTransactionalOutboxTestDataAsync(100);

            //Initialize Transaction and Outbox Processor
            await using var sqlConnection = await SqlConnectionHelper.CreateSystemDataSqlConnectionAsync();
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
