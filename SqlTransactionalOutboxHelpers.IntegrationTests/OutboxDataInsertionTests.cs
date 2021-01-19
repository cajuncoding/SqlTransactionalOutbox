using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlTransactionalOutboxHelpers.CustomExtensions;
using SqlTransactionalOutboxHelpers.SqlServer.SystemDataNS;
using SqlTransactionalOutboxHelpers.Tests;

namespace SqlTransactionalOutboxHelpers.IntegrationTests
{
    [TestClass]
    public class OutboxDataInsertionTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task TestNewOutboxItemInsertionBenchmark()
        {
            //Organize
            await using var sqlConnection = await SqlConnectionHelper.CreateSystemDataSqlConnectionAsync();
            var noOpPublisher = new NoOpSqlTransactionalOutboxPublisher();

            //WARM UP...
            var warmupTestItems = TestHelper.CreateTestStringOutboxItemData(1);
            await using var warmupTransaction = (SqlTransaction)await sqlConnection.BeginTransactionAsync().ConfigureAwait(false);
            var outboxProcessor = new SqlServerTransactionalOutboxProcessor<string>(warmupTransaction, noOpPublisher);

            var warmupTimer = Stopwatch.StartNew();
            var warmupResults = await outboxProcessor.InsertNewPendingOutboxItemsAsync(warmupTestItems).ConfigureAwait(false);
            await warmupTransaction.CommitAsync();
            warmupTimer.Stop();

            TestContext?.WriteLine($"Warm-up Inserted [{warmupResults.Count}] items in [{warmupTimer.Elapsed.ToElapsedTimeDescriptiveFormat()}].");

            //Execute... Full Test...
            var executionTestItems = TestHelper.CreateTestStringOutboxItemData(1000);
            await using var sqlTransaction2 = (SqlTransaction)await sqlConnection.BeginTransactionAsync().ConfigureAwait(false);
            outboxProcessor = new SqlServerTransactionalOutboxProcessor<string>(sqlTransaction2, noOpPublisher);

            var timer = Stopwatch.StartNew();
            var executionResults = await outboxProcessor.InsertNewPendingOutboxItemsAsync(executionTestItems).ConfigureAwait(false);
            await sqlTransaction2.CommitAsync();
            timer.Stop();
            
            TestContext?.WriteLine($"Benchmark Execution Inserted [{executionResults.Count}] items in [{timer.Elapsed.ToElapsedTimeDescriptiveFormat()}].");
        }

        [TestMethod]
        public async Task TestNewOutboxItemInsertionByBatch()
        {
            //Organize
            await using var sqlConnection = await SqlConnectionHelper.CreateSystemDataSqlConnectionAsync();

            //CLEAR the Table for Integration Tests to validate:
            await using var sqlCmd = new SqlCommand(
                $"TRUNCATE TABLE [{DefaultOutboxTableConfig.DefaultTransactionalOutboxSchemaName}].[{DefaultOutboxTableConfig.DefaultTransactionalOutboxTableName}]", 
                sqlConnection
            );
            await sqlCmd.ExecuteNonQueryAsync();

            //Initialize Transaction and Outbox Processor
            await using var sqlTransaction = (SqlTransaction)await sqlConnection.BeginTransactionAsync().ConfigureAwait(false);

            var noOpPublisher = new NoOpSqlTransactionalOutboxPublisher();
            var outboxProcessor = new SqlServerTransactionalOutboxProcessor<string>(sqlTransaction, noOpPublisher);

            var outboxTestItems = TestHelper.CreateTestStringOutboxItemData(100);

            //Execute
            var timer = Stopwatch.StartNew();
            
            var insertedResults = await outboxProcessor.InsertNewPendingOutboxItemsAsync(
                outboxTestItems
            ).ConfigureAwait(false);

            await sqlTransaction.CommitAsync();

            timer.Stop();
            TestContext?.WriteLine($"Inserted [{insertedResults.Count}] items in [{timer.Elapsed.ToElapsedTimeDescriptiveFormat()}].");

            //Assert
            Assert.AreEqual(insertedResults.Count, outboxTestItems.Count);
            var utcNow = DateTime.UtcNow;
            foreach (var result in insertedResults)
            {
                //Validate Created Date Time (can't match precisely but can validate it was populated as expected...
                Assert.AreEqual(result.Status, OutboxItemStatus.Pending);
                Assert.AreNotEqual(result.CreatedDateTimeUtc, DateTime.MinValue);
                Assert.AreEqual(result.PublishingAttempts, 0);
                Assert.IsFalse(string.IsNullOrWhiteSpace(result.PublishingTarget));
                Assert.IsFalse(string.IsNullOrWhiteSpace(result.PublishingPayload));
                Assert.IsTrue(result.UniqueIdentifier != Guid.Empty);
            }
        }

        [TestMethod]
        public async Task TestNewOutboxItemInsertionAndRetrieval()
        {
            //Organize
            await using var sqlConnection = await SqlConnectionHelper.CreateSystemDataSqlConnectionAsync();

            //CLEAR the Table for Integration Tests to validate:
            await using var sqlCmd = new SqlCommand(
                $"TRUNCATE TABLE [{DefaultOutboxTableConfig.DefaultTransactionalOutboxSchemaName}].[{DefaultOutboxTableConfig.DefaultTransactionalOutboxTableName}]",
                sqlConnection
            );
            await sqlCmd.ExecuteNonQueryAsync();

            //Initialize Transaction and Outbox Processor
            await using var sqlTransaction = (SqlTransaction)await sqlConnection.BeginTransactionAsync().ConfigureAwait(false);

            var noOpPublisher = new NoOpSqlTransactionalOutboxPublisher();
            var outboxProcessor = new SqlServerTransactionalOutboxProcessor<string>(sqlTransaction, noOpPublisher);

            var outboxTestItems = TestHelper.CreateTestStringOutboxItemData(100);

            //Execute
            var insertedResults = await outboxProcessor.InsertNewPendingOutboxItemsAsync(
                outboxTestItems
            ).ConfigureAwait(false);

            await sqlTransaction.CommitAsync();

            //RETRIEVE ALL Pending Items from the Outbox to validate
            var pendingResults = await outboxProcessor.OutboxRepository.RetrieveOutboxItemsAsync(
                OutboxItemStatus.Pending
            );

            //Assert
            Assert.AreEqual(insertedResults.Count, outboxTestItems.Count);

            var insertedItemsLookup = insertedResults.ToLookup(i => i.UniqueIdentifier);
            foreach (var pendingItem in pendingResults)
            {
                var insertedItem = insertedItemsLookup[pendingItem.UniqueIdentifier].FirstOrDefault();
                Assert.IsNotNull(pendingItem);
                Assert.AreEqual(pendingItem.Status, OutboxItemStatus.Pending);
                Assert.AreEqual(pendingItem.CreatedDateTimeUtc, insertedItem.CreatedDateTimeUtc);
                Assert.AreEqual(pendingItem.PublishingAttempts, insertedItem.PublishingAttempts);
                Assert.AreEqual(pendingItem.PublishingTarget, insertedItem.PublishingTarget);
                Assert.AreEqual(pendingItem.PublishingPayload, insertedItem.PublishingPayload);
            }
        }
    }
}
