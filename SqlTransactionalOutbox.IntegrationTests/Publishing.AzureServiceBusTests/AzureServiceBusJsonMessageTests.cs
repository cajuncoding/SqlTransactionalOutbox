using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using SqlTransactionalOutbox.Tests;
using SqlTransactionalOutbox.AzureServiceBus;
using SqlTransactionalOutbox.AzureServiceBus.Receiving;
using SqlTransactionalOutbox.SqlServer.MicrosoftDataNS;

namespace SqlTransactionalOutbox.IntegrationTests
{
    [TestClass]
    public class AzureServiceBusJsonMessageTests
    {
        public const string IntegrationTestTopic = "SqlTransactionalOutbox/Integration-Tests";
        public const string IntegrationTestSubscriptionName = "dev-local";
        public static TimeSpan IntegrationTestServiceBusDeliveryWaitTimeSpan = TimeSpan.FromSeconds(15);

        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task TestAzureServiceBusJsonPayloadDirectPublishing()
        {
            //*****************************************************************************************
            //* STEP 1 - Prepare the Test Outbox Item to be Published
            //*****************************************************************************************
            var testPayload = new
            {
                To = "CajunCoding",
                FifoGroupingId = nameof(TestAzureServiceBusJsonPayloadDirectPublishing),
                ContentType = MessageContentTypes.PlainText,
                Body = $"Testing publishing of Json Payload with PlainText Body for [{nameof(TestAzureServiceBusJsonPayloadDirectPublishing)}]!",
                Headers = new
                {
                    IntegrationTestName = nameof(TestAzureServiceBusJsonPayloadDirectPublishing),
                    IntegrationTestExecutionDateTime = DateTimeOffset.UtcNow,
                }
            };

            var jsonPayload = JsonConvert.SerializeObject(testPayload);
            var outboxItemFactory = new DefaultOutboxItemFactory<string>();
            var uniqueIdGuidFactory = outboxItemFactory.UniqueIdentifierFactory;

            var outboxItem = outboxItemFactory.CreateExistingOutboxItem(
                uniqueIdentifier:uniqueIdGuidFactory.CreateUniqueIdentifier().ToString(),
                createdDateTimeUtc: DateTimeOffset.UtcNow,
                status: OutboxItemStatus.Pending.ToString(),
                fifoGroupingIdentifier: testPayload.FifoGroupingId,
                publishAttempts: 0,
                publishTarget: IntegrationTestTopic,
                serializedPayload: jsonPayload
            );

            //*****************************************************************************************
            //* STEP 2 - Publish the receivedItem to Azure Service Bus!
            //*****************************************************************************************
            var azureServiceBusPublisher = new DefaultAzureServiceBusOutboxPublisher(
                TestConfiguration.AzureServiceBusConnectionString,
                new AzureServiceBusPublishingOptions()
                {
                    LogDebugCallback = s => TestContext.WriteLine(s),
                    LogErrorCallback = e => TestContext.WriteLine(e.Message + e.InnerException?.Message)
                }
            );

            //Execute the publish to Azure...
            outboxItem.PublishAttempts++;
            await azureServiceBusPublisher.PublishOutboxItemAsync(outboxItem);

            //NOTE: Because we manually incremented the outbox item and mutated it (e.g. vs deep clone initialized from the DB)
            //      we now need to decrement it for Test Assertions to work as expected against the deep clone that will be recieved
            //      from the Azure Event Bus...
            outboxItem.PublishAttempts--;

            //*****************************************************************************************
            //* STEP 3 - Attempt to Retrieve/Receive the Message & Validate after Arrival!
            //*****************************************************************************************
            await AssertReceiptAndValidationOfThePublishedItem(outboxItem);

        }

        [TestMethod]
        public async Task TestAzureServiceBusJsonPayloadPublishingViaOutboxExtensions()
        {
            //*****************************************************************************************
            //* STEP 1 - Prepare the Test Outbox Item to be Published
            //*****************************************************************************************
            var testPayload = new
            {
                PublishTarget = IntegrationTestTopic,
                To = "CajunCoding",
                FifoGroupingId = nameof(TestAzureServiceBusJsonPayloadDirectPublishing),
                ContentType = MessageContentTypes.PlainText,
                Body = $"Testing publishing of Json Payload with PlainText Body for [{nameof(TestAzureServiceBusJsonPayloadPublishingViaOutboxExtensions)}]!",
                Headers = new
                {
                    IntegrationTestName = nameof(TestAzureServiceBusJsonPayloadDirectPublishing),
                    IntegrationTestExecutionDateTime = DateTimeOffset.UtcNow
                }
            };

            var jsonText = JsonConvert.SerializeObject(testPayload);

            //*****************************************************************************************
            //* STEP 2 - Store the Dynamic Payload into the Outbox!
            //*****************************************************************************************
            var sqlConnection = await SqlConnectionHelper.CreateMicrosoftDataSqlConnectionAsync();

            await sqlConnection.TruncateTransactionalOutboxTableAsync();

            var outboxItem = await sqlConnection
                .AddTransactionalOutboxPendingItemAsync(jsonText)
                .ConfigureAwait(false);


            //*****************************************************************************************
            //* STEP 3 - Now Process the Outbox to Publish the items data
            //*****************************************************************************************
            var azureServiceBusPublisher = new DefaultAzureServiceBusOutboxPublisher(
                TestConfiguration.AzureServiceBusConnectionString,
                new AzureServiceBusPublishingOptions()
                {
                    LogDebugCallback = s => TestContext.WriteLine(s),
                    LogErrorCallback = e => TestContext.WriteLine(e.Message + e.InnerException?.Message)
                }
            );

            var processedResults = await sqlConnection.ProcessPendingOutboxItemsAsync(azureServiceBusPublisher, new OutboxProcessingOptions()
            {
                FifoEnforcedPublishingEnabled = true,
                LogDebugCallback = s => TestContext.WriteLine(s),
                LogErrorCallback = e => TestContext.WriteLine(e.Message + e.InnerException?.Message),
                MaxPublishingAttempts = 1,
                TimeSpanToLive = TimeSpan.FromMinutes(5)
            });

            //*****************************************************************************************
            //* STEP 4 - Attempt to Retrieve/Receive the Message & Validate after Arrival!
            //*****************************************************************************************
            await AssertReceiptAndValidationOfThePublishedItem(outboxItem);
        }

        private async Task AssertReceiptAndValidationOfThePublishedItem(ISqlTransactionalOutboxItem<Guid> outboxItem)
        {
            //*****************************************************************************************
            //* Attempt to Retrieve/Receive the Message & Validate after Arrival!
            //*****************************************************************************************
            var azureServiceBusReceiver = new DefaultFifoAzureServiceBusReceiver<string>(
                TestConfiguration.AzureServiceBusConnectionString, 
                options: new AzureServiceBusReceivingOptions()
                {
                    LogDebugCallback = (message) => Debug.WriteLine(message)//,
                    //LogErrorCallback = (exc) => TestContext.WriteLine(exc.Message + exc.InnerException?.Message)
                }
            );

            int itemProcessedCount = 0;

            try
            {
                await foreach (var item in azureServiceBusReceiver.RetrieveAsyncEnumerable(
                    IntegrationTestTopic, IntegrationTestSubscriptionName, IntegrationTestServiceBusDeliveryWaitTimeSpan)
                )
                {
                    Assert.IsNotNull(item, $"The received published outbox receivedItem is null! This should never happen!");
                    TestContext.Write($"Received receivedItem from Azure Service Bus receiver queue [{item.PublishedItem.UniqueIdentifier}]...");

                    //*****************************************************************************************
                    //* Validate the Item when it is detected/matched!
                    //*****************************************************************************************
                    if (item.PublishedItem.UniqueIdentifier == outboxItem.UniqueIdentifier)
                    {
                        //Finalize Status of the receivedItem we published as Successfully Received! 
                        await item.AcknowledgeSuccessfulReceiptAsync();
                        itemProcessedCount++;

                        //VALIDATE the Matches of original Payload, Inserted item, and Received/Published Data!
                        TestHelper.AssertOutboxItemMatchesReceivedItem(outboxItem, item);

                        //We found the Item we expected, and all tests passed so we can break out and FINISH this test!
                        break;
                    }
                    else
                    {
                        await item.RejectAsDeadLetterAsync();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Assert.Fail("The receivedItem published to Azure Service Bus was never received fore timing out!");
            }

            Assert.IsTrue(itemProcessedCount > 0, "We should have processed at least the one receivedItem we published!");

        }
    }
}
