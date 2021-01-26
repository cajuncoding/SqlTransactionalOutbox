using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlTransactionalOutbox.SqlServer.SystemDataNS;
using SqlTransactionalOutbox.Tests;

namespace SqlTransactionalOutbox.IntegrationTests
{
    [TestClass]
    public class OutboxEndToEndSuccessfulTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task TestTransactionalOutboxEndToEndSuccessfulProcessing()
        {
            //Organize
            await using var sqlConnection = await SqlConnectionHelper.CreateSystemDataSqlConnectionAsync();

            //*****************************************************************************************
            //* STEP 1 - Prepare/Clear the Queue Table
            //*****************************************************************************************
            var publishedItemList = new List<ISqlTransactionalOutboxItem<Guid>>();
            var testPublisher = new TestHarnessSqlTransactionalOutboxPublisher(i =>
            {
                publishedItemList.Add(i);
                TestContext.WriteLine($"Successfully Published Item: {i.UniqueIdentifier}");
                return Task.CompletedTask;
            });

            await SystemDataSqlTestHelpers.PopulateTransactionalOutboxTestDataAsync(100, testPublisher);

            //*****************************************************************************************
            //* STEP 3 - Executing processing of the Pending Items in the Queue...
            //*****************************************************************************************
            //Execute Processing of Items just inserted!
            //NOTE: We need to re-initialize a NEW Transaction and Processor to correctly simulate this running separately!
            await using var sqlTransaction2 = (SqlTransaction) await sqlConnection.BeginTransactionAsync().ConfigureAwait(false);
            var outboxProcessor = new DefaultSqlServerOutboxProcessor<string>(sqlTransaction2, testPublisher);

            var publishedResults = await outboxProcessor.ProcessPendingOutboxItemsAsync().ConfigureAwait(false);

            await sqlTransaction2.CommitAsync();

            //Assert results
            Assert.AreEqual(publishedItemList.Count, publishedResults.SuccessfullyPublishedItems.Count);
            Assert.AreEqual(0, publishedResults.FailedItems.Count);

            //Assert Unique Items all match
            var publishedItemLookup = publishedItemList.ToLookup(i => i.UniqueIdentifier);
            publishedResults.SuccessfullyPublishedItems.ForEach(r =>
            {
                Assert.IsTrue(publishedItemLookup.Contains(r.UniqueIdentifier));
            });

            //*****************************************************************************************
            //* STEP 4 - Retrieve and Validate Data is updated and no pending Items Remain...
            //*****************************************************************************************
            //Assert All Items in the DB are Successful!
            await using var sqlTransaction3 = (SqlTransaction) await sqlConnection.BeginTransactionAsync().ConfigureAwait(false);
            outboxProcessor = new DefaultSqlServerOutboxProcessor<string>(sqlTransaction3, testPublisher);

            var successfulResultsFromDb = await outboxProcessor.OutboxRepository
                .RetrieveOutboxItemsAsync(OutboxItemStatus.Successful)
                .ConfigureAwait(false);

            //Assert the results from the DB match those returned from the Processing method...
            Assert.AreEqual(publishedResults.SuccessfullyPublishedItems.Count, successfulResultsFromDb.Count);
            successfulResultsFromDb.ForEach(dbItem =>
            {
                Assert.AreEqual(OutboxItemStatus.Successful, dbItem.Status);
                Assert.AreEqual(1, dbItem.PublishAttempts);
            });
        }

    }
}