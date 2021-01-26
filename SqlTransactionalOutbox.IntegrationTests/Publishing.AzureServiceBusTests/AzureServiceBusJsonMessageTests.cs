using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlTransactionalOutbox.Tests;
using SqlTransactionalOutbox.AzureServiceBus;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.Interfaces;

namespace SqlTransactionalOutbox.IntegrationTests
{
    [TestClass]
    public class AzureServiceBusJsonMessageTests
    {
        public const string IntegrationTestTopic = "SqlTransactionalOutbox/Integration-Tests";
        public const string IntegrationTestSubscriptionName = "dev-local";

        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task TestAzureServiceBusJsonPayloadPublishing()
        {
            //*****************************************************************************************
            //* STEP 1 - Prepare/Clear the Test Outbox Item to be Published
            //*****************************************************************************************
            var testPayload = new
            {
                To = "CajunCoding",
                FifoGroupingId = nameof(TestAzureServiceBusJsonPayloadPublishing),
                ContentType = MessageContentTypes.PlainText,
                Body = "Testing publishing of Json Payload with PlainText Body!",
                Headers = new
                {
                    IntegrationTestName = nameof(TestAzureServiceBusJsonPayloadPublishing),
                    IntegrationTestExecutionDateTime = DateTime.UtcNow,
                }
            };

            var jsonPayload = JsonConvert.SerializeObject(testPayload);
            var uniqueIdGuidFactory = new OutboxGuidUniqueIdentifier();
            var outboxItemFactory = new OutboxItemFactory<Guid, string>(uniqueIdGuidFactory);

            var outboxItem = outboxItemFactory.CreateExistingOutboxItem(
                uniqueIdGuidFactory.CreateUniqueIdentifier().ToString(),
                DateTime.UtcNow,
                OutboxItemStatus.Pending.ToString(),
                0,
                IntegrationTestTopic,
                jsonPayload
            );

            //*****************************************************************************************
            //* STEP 2 - Publish the item to Azure Service Bus!
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
            await azureServiceBusPublisher.PublishOutboxItemAsync(outboxItem);

            //*****************************************************************************************
            //* STEP 3 - Initialize Callback Receiver and attempt to Retrieve/Receive the Message after Arrival!
            //*****************************************************************************************
            var azureServiceBusReceiver = new DefaultFifoAzureServiceBusReceiver<string>(TestConfiguration.AzureServiceBusConnectionString);

            int itemProcessedCount = 0;

            try
            {
                await foreach (var item in azureServiceBusReceiver.RetrieveAsyncEnumerable(
                    IntegrationTestTopic, IntegrationTestSubscriptionName, TimeSpan.FromSeconds(60))
                )
                {
                    Assert.IsNotNull(item, $"The received published outbox item is null! This should never happen!");
                    TestContext.Write($"Received item from Azure Service Bus receiver queue [{item.PublishedItem.UniqueIdentifier}]...");

                    if (item.PublishedItem.UniqueIdentifier == outboxItem.UniqueIdentifier)
                    {
                        //Finalize Status of the item we published as Successfully Received! 
                        await item.AcknowledgeSuccessfulReceiptAsync();

                        itemProcessedCount++;

                        var publishedOutboxItem = item.PublishedItem;
                        Assert.AreEqual(testPayload.FifoGroupingId, item.FifoGroupingIdentifier);
                        Assert.AreEqual(outboxItem.UniqueIdentifier, publishedOutboxItem.UniqueIdentifier);
                        Assert.AreEqual(testPayload.Body, publishedOutboxItem.Payload, "The Outbox Item Body was not correctly resolved as the Published Item's Payload");
                        Assert.IsTrue((outboxItem.CreatedDateTimeUtc - publishedOutboxItem.CreatedDateTimeUtc).TotalMilliseconds < 100, "The Created Date Time values are greater than 100ms different.");
                        Assert.AreEqual(OutboxItemStatus.Successful, publishedOutboxItem.Status);
                        Assert.AreEqual(outboxItem.PublishAttempts, publishedOutboxItem.PublishAttempts);

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
                Assert.Fail("The item published to Azure Service Bus was never received fore timing out!");
            }

            Assert.IsTrue(itemProcessedCount > 0, "We should have processed at least the one item we published!");
        }
    }
}
