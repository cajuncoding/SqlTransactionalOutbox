﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlTransactionalOutbox.SqlServer.SystemDataNS;
using SqlTransactionalOutbox.Tests;

namespace SqlTransactionalOutbox.IntegrationTests.SystemDataNS
{
    [TestClass]
    public class OutboxEndToEndFailureTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task TestTransactionalOutboxFailureByTimeToLive()
        {
            var failedItemTestDataSizeByBatch = 3;
            var successfulItemTestDataSize = 3;
            var timeToLiveTimeSpan = TimeSpan.FromSeconds(5);
            var testHarnessPublisher = new TestHarnessSqlTransactionalOutboxPublisher();

            var expectedTotalSuccessCount = 0;
            var expectedTotalExpiredCount = 0;

            //*****************************************************************************************
            //* STEP 1 - Prepare/Clear the Queue Table and populate initial Set of items (expected to Fail/Expire)
            //              then wait for them to expire...
            //*****************************************************************************************

            await SystemDataSqlTestHelpers.PopulateTransactionalOutboxTestDataAsync(failedItemTestDataSizeByBatch);
            expectedTotalExpiredCount += failedItemTestDataSizeByBatch;

            await Task.Delay(timeToLiveTimeSpan + TimeSpan.FromSeconds(3));

            //*****************************************************************************************
            //* STEP 2 - Add a Second & Third batch of items with different insertion Timestamps to
            //              ensure only the first set expire as expected...
            //*****************************************************************************************
            await SystemDataSqlTestHelpers.PopulateTransactionalOutboxTestDataAsync(successfulItemTestDataSize, false);
            expectedTotalSuccessCount += successfulItemTestDataSize;

            //Insert in a second batch to force different Creation Dates at the DB level...
            await SystemDataSqlTestHelpers.PopulateTransactionalOutboxTestDataAsync(successfulItemTestDataSize, false);
            expectedTotalSuccessCount += successfulItemTestDataSize;

            //*****************************************************************************************
            //* STEP 2 - Process Outbox and get Results
            //*****************************************************************************************
            await using var sqlConnection = await SqlConnectionHelper.CreateSystemDataSqlConnectionAsync();
            var processingResults = await sqlConnection.ProcessPendingOutboxItemsAsync(testHarnessPublisher, new OutboxProcessingOptions()
            {
                TimeSpanToLive = timeToLiveTimeSpan
            });

            //*****************************************************************************************
            //* STEP 3 - Validate Results returned!
            //*****************************************************************************************
            Assert.AreEqual(expectedTotalExpiredCount, processingResults.FailedItems.Count);
            Assert.AreEqual(expectedTotalSuccessCount, processingResults.SuccessfullyPublishedItems.Count);

            //We expect all items to be processed before any item is failed....
            //So the First Item will be repeated as the 10'th item after the next 9 are also attempted...
            processingResults.SuccessfullyPublishedItems.ForEach(i =>
            {
                Assert.AreEqual(OutboxItemStatus.Successful, i.Status);
            });

            processingResults.FailedItems.ForEach(i =>
            {
                Assert.AreEqual(OutboxItemStatus.FailedExpired, i.Status);
            });

            //*****************************************************************************************
            //* STEP 3 - Validate Results In the DB!
            //*****************************************************************************************
            await using var sqlTransaction2 = (SqlTransaction)await sqlConnection.BeginTransactionAsync().ConfigureAwait(false);
            var outboxProcessor = new DefaultSqlServerTransactionalOutboxProcessor<string>(sqlTransaction2, testHarnessPublisher);
            var outboxRepository = outboxProcessor.OutboxRepository;

            var successfulItems = await outboxRepository.RetrieveOutboxItemsAsync(OutboxItemStatus.Successful);
            Assert.AreEqual(expectedTotalSuccessCount, successfulItems.Count);
            successfulItems.ForEach(i =>
            {
                Assert.AreEqual(OutboxItemStatus.Successful, i.Status);
            });

            var failedItems = await outboxRepository.RetrieveOutboxItemsAsync(OutboxItemStatus.FailedExpired);
            Assert.AreEqual(expectedTotalExpiredCount, failedItems.Count);
            processingResults.FailedItems.ForEach(i =>
            {
                Assert.AreEqual(OutboxItemStatus.FailedExpired, i.Status);
            });
        }

        [TestMethod]
        public async Task TestTransactionalOutboxEventualRecoveryOfBlockingByExceptionFailureItems()
        {
            var testDataSize = 5;
            var maxPublishingAttempts = 2;
            var failedItemsFromDb = await DoTestTransactionalOutboxCrawlingOfBlockingFailureItems(
                testDataSize,
                maxPublishingAttempts,
                false,
                true
            );

            //We expect all items to be processed before any item is failed....
            //So the First Item will be repeated as the 10'th item after the next 9 are also attempted...
            for (var x = 0; x < testDataSize * maxPublishingAttempts; x += 2)
            {
                Assert.AreEqual(failedItemsFromDb[x].UniqueIdentifier, failedItemsFromDb[x + 1].UniqueIdentifier);
            }
        }

        [TestMethod]
        public async Task TestTransactionalOutboxEventualRecoveryOfFifoBlockingFailureItems()
        {
            var testDataSize = 5;
            var maxPublishingAttempts = 2;
            var failedItemsFromDb = await DoTestTransactionalOutboxCrawlingOfBlockingFailureItems(
                testDataSize,
                maxPublishingAttempts, 
                true, 
                false
            );

            //We expect all items to be processed before any item is failed....
            //So the First Item will be repeated as the 10'th item after the next 9 are also attempted...
            for (var x = 0; x < testDataSize * maxPublishingAttempts; x+=2)
            {
                Assert.AreEqual(failedItemsFromDb[x].UniqueIdentifier, failedItemsFromDb[x + 1].UniqueIdentifier);
            }
        }

        [TestMethod]
        public async Task TestTransactionalOutboxEventualRecoveryOfNonBlockingFailureItems()
        {
            var testDataSize = 5;
            var failedItemsFromDb = await DoTestTransactionalOutboxCrawlingOfBlockingFailureItems(
                testDataSize,
                2,
                false, 
                false
            );

            //We expect all items to be processed before any item is failed....
            //So the First Item will be repeated as the 10'th item after the next 9 are also attempted...
            for (var x = 0; x < testDataSize; x++)
            {
                Assert.AreEqual(failedItemsFromDb[x].UniqueIdentifier, failedItemsFromDb[x + testDataSize].UniqueIdentifier);
            }
        }

        public async Task<List<ISqlTransactionalOutboxItem<Guid>>> DoTestTransactionalOutboxCrawlingOfBlockingFailureItems(
            int testDataSize, int maxPublishingAttempts, bool enforceFifoProcessing, bool throwExceptionOnFailedItem)
        {
            //*****************************************************************************************
            //* STEP 1 - Prepare/Clear the Queue Table
            //*****************************************************************************************
            await SystemDataSqlTestHelpers.PopulateTransactionalOutboxTestDataAsync(testDataSize);

            //*****************************************************************************************
            //* STEP 2 - Setup Custom Publisher & Processing Options...
            //*****************************************************************************************
            var publishedAttemptsList = new List<ISqlTransactionalOutboxItem<Guid>>();
            var failingPublisher = new TestHarnessSqlTransactionalOutboxPublisher(
                (i, isFifoEnabled) =>
                {
                    publishedAttemptsList.Add(i);
                    TestContext.WriteLine($"Successful -- We have intentionally Failed to Publish Item: {i.UniqueIdentifier}");
                    //Force an Error on Failure... this should result in ALL Publishing attempts to fail...
                    throw new Exception("Failed to Publish!");
                }
            );

            var outboxProcessingOptions = new OutboxProcessingOptions()
            {
                MaxPublishingAttempts = maxPublishingAttempts,
                FifoEnforcedPublishingEnabled = enforceFifoProcessing
            };

            //*****************************************************************************************
            //* STEP 3 - Executing processing of the Pending Items in the Queue...
            //*****************************************************************************************
            //Execute Processing of Items just inserted!
            //NOTE: We need to re-initialize a NEW Transaction and Processor to correctly simulate this running separately!
            ISqlTransactionalOutboxProcessingResults<Guid> publishedResults = null;
            int loopCounter = 0;
            bool handledExceptionSoItsOkToContinue = false;
            do
            {
                await using var sqlConnection = await SqlConnectionHelper.CreateSystemDataSqlConnectionAsync();
                await using var sqlTransaction = (SqlTransaction)await sqlConnection.BeginTransactionAsync().ConfigureAwait(false);

                handledExceptionSoItsOkToContinue = false;
                try
                {
                    publishedResults = await sqlTransaction.ProcessPendingOutboxItemsAsync(
                        failingPublisher, 
                        outboxProcessingOptions,
                        throwExceptionOnFailedItem
                    ).ConfigureAwait(false);
                }
                catch (Exception exc)
                {
                    if (throwExceptionOnFailedItem)
                    {
                        //DO Nothing, as we Expect there to be exceptions when Throw Exception on Failure is Enabled!
                        TestContext.WriteLine($"Successfully handled expected Exception: {exc.Message}");
                        publishedResults = new OutboxProcessingResults<Guid>();
                        handledExceptionSoItsOkToContinue = true;
                    }
                    else
                    {
                        //IF we get an exception but ThrowExceptionOnFailure is disabled, then this is an issue!
                        throw;
                    }
                }

                await sqlTransaction.CommitAsync();

                //Provide Infinite Loop fail-safe...
                loopCounter++;
                Assert.IsTrue(loopCounter <= (testDataSize * maxPublishingAttempts * 2), $"Infinite Loop Breaker Tripped at [{loopCounter}]!");

                //Assert there are never any successfully published items...
                Assert.AreEqual(0, publishedResults.SuccessfullyPublishedItems.Count);

            } while (publishedResults.FailedItems.Count > 0 || (throwExceptionOnFailedItem && handledExceptionSoItsOkToContinue));

            //*****************************************************************************************
            //* STEP 4 - Retrieve and Validate Data in the Database is updated and all have failed out...
            //*****************************************************************************************
            //Assert All Items in the DB are Successful!
            await using var sqlConnection2 = await SqlConnectionHelper.CreateSystemDataSqlConnectionAsync();
            await using var sqlTransaction2 = (SqlTransaction)await sqlConnection2.BeginTransactionAsync().ConfigureAwait(false);
            var outboxProcessor2 = new DefaultSqlServerTransactionalOutboxProcessor<string>(sqlTransaction2, failingPublisher);

            var successfulResultsFromDb = await outboxProcessor2.OutboxRepository
                .RetrieveOutboxItemsAsync(OutboxItemStatus.Pending)
                .ConfigureAwait(false);

            //Assert the results from the DB match those returned from the Processing method...
            Assert.AreEqual(successfulResultsFromDb.Count, 0);

            var failedResultsFromDb = await outboxProcessor2.OutboxRepository
                .RetrieveOutboxItemsAsync(OutboxItemStatus.FailedAttemptsExceeded)
                .ConfigureAwait(false);

            //Assert the results from the DB match those returned from the Processing method...
            Assert.AreEqual(failedResultsFromDb.Count * maxPublishingAttempts, publishedAttemptsList.Count);
            foreach (var dbItem in failedResultsFromDb)
            {
                Assert.AreEqual(OutboxItemStatus.FailedAttemptsExceeded, dbItem.Status);
                Assert.AreEqual(maxPublishingAttempts, dbItem.PublishAttempts);
            }

            //RETURN the Attempted Publishing list for additional validation based on the Pattern
            //  we expect for items to be processed.
            return publishedAttemptsList;
        }
    }
}