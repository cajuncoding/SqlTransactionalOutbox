using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlTransactionalOutbox.SqlServer.SystemDataNS;
using SqlTransactionalOutbox.Tests;

namespace SqlTransactionalOutbox.IntegrationTests.SystemDataNS
{
    [TestClass]
    public class OutboxEndToEndSuccessfulTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task TestTransactionalOutboxEndToEndSuccessfulForImmediateItemProcessingOnly()
        {
            //Organize
            await using var sqlConnection = await SqlConnectionHelper.CreateSystemDataSqlConnectionAsync().ConfigureAwait(false);

            var scheduledPublishDelayTime = TimeSpan.FromSeconds(30);

            var immediateDeliveryItemCount = 100;
            var scheduledDeliveryItemCount = 10;

            //*****************************************************************************************
            //* STEP 1 - Prepare/Clear the Queue Table and re-add valid test data...
            //*****************************************************************************************

            //First add normal Outbox items to be immediately published/delivered...
            await SystemDataSqlTestHelpers.PopulateTransactionalOutboxTestDataAsync(immediateDeliveryItemCount, clearExistingOutbox: true);

            //Second add scheduled Outbox items to be published/delivered in the future...
            await MicrosoftDataSqlTestHelpers.PopulateTransactionalOutboxTestDataAsync(
                scheduledDeliveryItemCount,
                clearExistingOutbox: false,
                scheduledPublishDateTime: DateTime.UtcNow.Add(scheduledPublishDelayTime)
            );

            //*****************************************************************************************
            //* STEP 3 - Executing processing of the Pending Items in the Queue...
            //*****************************************************************************************
            //Execute Processing of Items just inserted!
            //NOTE: We need to re-initialize a NEW Transaction and Processor to correctly simulate this running separately!
            var publishedItemList = new List<ISqlTransactionalOutboxItem<Guid>>();
            var testPublisher = new TestHarnessSqlTransactionalOutboxPublisher(
                publishingAction: (item, isFifoEnabled) =>
                {
                    publishedItemList.Add(item);
                    TestContext.WriteLine($"Successfully Published Item: {item.UniqueIdentifier}");
                    return Task.CompletedTask;
                }
            );

            //NOTE: We do our first processing pass with Default Configuration options which will only pick up the items
            //  that are immediately available for publish (i.e. not scheduled for the future)...
            var publishedResults = await sqlConnection
                .ProcessPendingOutboxItemsAsync(testPublisher)
                .ConfigureAwait(false);

            //Assert results
            Assert.AreEqual(immediateDeliveryItemCount, publishedResults.SuccessfullyPublishedItems.Count);
            Assert.AreEqual(publishedItemList.Count, publishedResults.SuccessfullyPublishedItems.Count);
            Assert.AreEqual(0, publishedResults.FailedItems.Count);

            //Assert Unique Items all exist, match exactly, and were not duplicated, etc.
            var publishedItemLookup = publishedItemList.ToLookup(i => i.UniqueIdentifier);
            publishedResults.SuccessfullyPublishedItems.ForEach(r =>
            {
                Assert.IsTrue(publishedItemLookup.Contains(r.UniqueIdentifier));
                Assert.AreEqual(1, publishedItemLookup[r.UniqueIdentifier].Count());
            });

            //*****************************************************************************************
            //* STEP 4 - Retrieve and Validate Data is updated and no pending Items Remain...
            //*****************************************************************************************
            //Assert All Items in the DB are Successful!
            await using var sqlTransaction3 = (SqlTransaction)await sqlConnection.BeginTransactionAsync().ConfigureAwait(false);
            var outboxProcessor = new DefaultSqlServerTransactionalOutboxProcessor<string>(sqlTransaction3, testPublisher);

            //NOTE: We for VALIDATION check only Successful items should be returned which will exclude the Scheduled future items still pending!
            var successfulResultsFromDb = await outboxProcessor.OutboxRepository
                .RetrieveOutboxItemsAsync(OutboxItemStatus.Successful)
                .ConfigureAwait(false);

            //Assert the results from the DB match those returned from the Processing method and are correctly set in the DB
            //  as Successful, with 1 publish Attempt, etc...
            Assert.AreEqual(immediateDeliveryItemCount, successfulResultsFromDb.Count);
            Assert.AreEqual(publishedResults.SuccessfullyPublishedItems.Count, successfulResultsFromDb.Count);
            successfulResultsFromDb.ForEach(dbItem =>
            {
                Assert.AreEqual(OutboxItemStatus.Successful, dbItem.Status);
                Assert.AreEqual(1, dbItem.PublishAttempts);
            });

            //*****************************************************************************************
            //* STEP 5 - Vadate that Future Schdeduled Items are still Pending!
            //*****************************************************************************************
            //Assert All Items in the DB are Successful!
            //NOTE: To validate this we use the Prefetch Feature that allows us to look ahead and pull in Scheduled items!
            var pendingScheduledResultsFromDb = await outboxProcessor.OutboxRepository
                .RetrieveOutboxItemsAsync(OutboxItemStatus.Pending, scheduledPublishPrefetchTime: (scheduledPublishDelayTime * 2))
                .ConfigureAwait(false);

            //Assert the results from the DB match those returned from the Processing method and are correctly set in the DB
            //  as Successful, with 1 publish Attempt, etc...
            Assert.AreEqual(scheduledDeliveryItemCount, pendingScheduledResultsFromDb.Count);
        }

        [TestMethod]
        public async Task TestTransactionalOutboxEndToEndSuccessfulForScheduledItemProcessing()
        {
            //Organize
            await using var sqlConnection = await SqlConnectionHelper.CreateSystemDataSqlConnectionAsync().ConfigureAwait(false);

            var scheduledPublishDelayTime = TimeSpan.FromSeconds(10);
            //We add a buffer here to account for any variance between the App and DB clocks, etc.
            //to ensure we are safely past the scheduled publish time before processing!
            var waitTimeVarianceBufferBetweenAppAndDB = TimeSpan.FromSeconds(2);

            var immediateDeliveryItemCount = 5;
            var scheduledDeliveryItemCount = 25;
            var totalExpectedDeliveryItemCount = immediateDeliveryItemCount + scheduledDeliveryItemCount;

            //Update the global default Options here to ensure that processing of the Outbox does a Prefetch of Scheduled items
            //  to pull them in ahead of real scheduled time and ensure they are included (e.g. will still be scheduled with pushed to Azure Service Bus)
            //  allowing us to validate this use case of global option update and functionality of the prefetch feature to pull in scheduled items
            //  for processing ahead of their scheduled publish time!
            OutboxProcessingOptions.DefaultOutboxProcessingOptions = new OutboxProcessingOptions()
            {
                //Set to Zero to rely on actual scheduled publish time and validate that scheduled items are not pulled in early for processing!
                ScheduledPublishPrefetchTime = TimeSpan.FromSeconds(30)
            };

            //*****************************************************************************************
            //* STEP 1 - Prepare/Clear the Queue Table and re-add valid test data...
            //*****************************************************************************************

            //First add normal Outbox items to be immediately published/delivered...
            await MicrosoftDataSqlTestHelpers.PopulateTransactionalOutboxTestDataAsync(immediateDeliveryItemCount, clearExistingOutbox: true);

            //Second add scheduled Outbox items to be published/delivered in the future...
            await MicrosoftDataSqlTestHelpers.PopulateTransactionalOutboxTestDataAsync(
                scheduledDeliveryItemCount,
                clearExistingOutbox: false,
                scheduledPublishDateTime: DateTime.UtcNow.Add(scheduledPublishDelayTime)
            );

            //We test the Prefetch queries in a number of other tests so here we want to test the default Scheduled Item processing
            //  which relies on the actual scheduled publish time and no prefetch time (do not look forward) feature to pull in early for processing,
            //  so we must wait here for the scheduled time to pass before we do our processing to validate that the scheduled items are correctly
            //  processed once their scheduled publish time has arrived!
            //Let's wait a bit.... 
            await Task.Delay(scheduledPublishDelayTime + waitTimeVarianceBufferBetweenAppAndDB);

            //*****************************************************************************************
            //* STEP 3 - Executing processing of the Pending Items in the Queue...
            //*****************************************************************************************
            //Execute Processing of Items just inserted!
            //NOTE: We need to re-initialize a NEW Transaction and Processor to correctly simulate this running separately!
            var publishedItemList = new List<ISqlTransactionalOutboxItem<Guid>>();
            var testPublisher = new TestHarnessSqlTransactionalOutboxPublisher(
                publishingAction: (item, isFifoEnabled) =>
                {
                    publishedItemList.Add(item);
                    TestContext.WriteLine($"Successfully Published Item: {item.UniqueIdentifier}");
                    return Task.CompletedTask;
                }
            );

            //NOTE: We do our first processing pass with Default Configuration options which will only pick up the items
            //  that are immediately available for publish (i.e. not scheduled for the future)...
            var publishedResults = await sqlConnection
                .ProcessPendingOutboxItemsAsync(testPublisher)
                .ConfigureAwait(false);

            //Assert results
            Assert.AreEqual(totalExpectedDeliveryItemCount, publishedResults.SuccessfullyPublishedItems.Count);
            Assert.AreEqual(publishedItemList.Count, publishedResults.SuccessfullyPublishedItems.Count);
            Assert.AreEqual(0, publishedResults.FailedItems.Count);

            //Assert Unique Items all exist, match exactly, and were not duplicated, etc.
            var publishedItemLookup = publishedItemList.ToLookup(i => i.UniqueIdentifier);
            publishedResults.SuccessfullyPublishedItems.ForEach(r =>
            {
                Assert.IsTrue(publishedItemLookup.Contains(r.UniqueIdentifier));
                Assert.AreEqual(1, publishedItemLookup[r.UniqueIdentifier].Count());
            });

            //*****************************************************************************************
            //* STEP 4 - Retrieve and Validate Data is updated and no pending Items Remain...
            //*****************************************************************************************
            //Assert All Items in the DB are Successful!
            await using var sqlTransaction3 = (SqlTransaction)await sqlConnection.BeginTransactionAsync().ConfigureAwait(false);
            var outboxProcessor = new DefaultSqlServerTransactionalOutboxProcessor<string>(sqlTransaction3, testPublisher);

            //NOTE: We for VALIDATION check only Successful items should be returned which will exclude the Scheduled future items still pending!
            var successfulResultsFromDb = await outboxProcessor.OutboxRepository
                .RetrieveOutboxItemsAsync(OutboxItemStatus.Successful)
                .ConfigureAwait(false);

            //Assert the results from the DB match those returned from the Processing method and are correctly set in the DB
            //  as Successful, with 1 publish Attempt, etc...
            Assert.AreEqual(totalExpectedDeliveryItemCount, successfulResultsFromDb.Count);
            Assert.AreEqual(publishedResults.SuccessfullyPublishedItems.Count, successfulResultsFromDb.Count);
            successfulResultsFromDb.ForEach(dbItem =>
            {
                Assert.AreEqual(OutboxItemStatus.Successful, dbItem.Status);
                Assert.AreEqual(1, dbItem.PublishAttempts);
            });

            //*****************************************************************************************
            //* STEP 5 - Vadate that there are NO MORE Future Schdeduled Items are still Pending!
            //*****************************************************************************************
            //Assert All Items in the DB are Successful!
            //NOTE: To validate this we use the Prefetch Feature that allows us to look ahead and pull in Scheduled items!
            var pendingScheduledResultsFromDb = await outboxProcessor.OutboxRepository
                .RetrieveOutboxItemsAsync(OutboxItemStatus.Pending, scheduledPublishPrefetchTime: (scheduledPublishDelayTime * 2))
                .ConfigureAwait(false);

            //Assert the results from the DB match those returned from the Processing method and are correctly set in the DB
            //  as Successful, with 1 publish Attempt, etc...
            Assert.AreEqual(0, pendingScheduledResultsFromDb.Count);

            //FINALLY REVERT the Global configruation changes so they don't impact other tests!
            OutboxProcessingOptions.ConfigureDefaults(options =>
            {
                //DO NOTHING all Options are initialized to defaults...
            });
        }

    }
}