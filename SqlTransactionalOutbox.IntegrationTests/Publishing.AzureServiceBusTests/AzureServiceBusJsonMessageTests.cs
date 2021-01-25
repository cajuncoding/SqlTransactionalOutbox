using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlTransactionalOutbox.Tests;
using SqlTransactionalOutbox.AzureServiceBus;
using SqlTransactionalOutbox.CustomExtensions;

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
            var jsonPayload = JsonConvert.SerializeObject(new
            {
                To = "CajunCoding",
                ContentType = MessageContentTypes.PlainText,
                Body = "Testing publishing of Json Payload with PlainText Body!",
                Headers = new
                {
                    IntegrationTestName = nameof(TestAzureServiceBusJsonPayloadPublishing),
                    IntegrationTestExecutionDateTime = DateTime.UtcNow,
                }
            });

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
            //* STEP 2 - Initialize Callback Receiver and attempt to Wait for the Message to Arrive!
            //*****************************************************************************************
            var azureServiceBusReceiver = new DefaultAzureServiceBusReceiver<string>(TestConfiguration.AzureServiceBusConnectionString);

            bool messageReceivedSuccessfully = false;

            //Attempt to Receive & Handle the Messages just published for End-to-End Validation!
            azureServiceBusReceiver.RegisterReceiverHandler(
                IntegrationTestTopic, 
                IntegrationTestSubscriptionName,
                receivedItemHandlerAsyncFunc: (receivedMessage) =>
                {
                    var receivedOutboxItem = receivedMessage.ReceivedItem;
                    TestHelper.AssertOutboxItemsMatch(outboxItem, receivedOutboxItem);

                    //ENABLE/NOTIFY for Continuation to complete the TEST!
                    //THis would be any other custom logic for the Receiver...
                    messageReceivedSuccessfully = true;

                    //Finally once ready we acknowledge and complete the Task so that it will not be re-sent again!
                    return Task.FromResult(OutboxReceivedItemProcessingStatus.AcknowledgeSuccessfulReceipt);
                }
            );

            //TODO: Decide if adding this ability to wait for a message Event to arrive, would be helpful as core functionality???
            var waitTimer = Stopwatch.StartNew();
            var timeoutSeconds = 10;
            while (!messageReceivedSuccessfully || waitTimer.Elapsed.TotalSeconds < timeoutSeconds)
            {
                await Task.Delay(500);
                TestContext.Write($"Waiting for Azure Service Bus to relay the message [{waitTimer.Elapsed.ToElapsedTimeDescriptiveFormat()}]...");
            }

            Assert.IsTrue(messageReceivedSuccessfully, $"Timeout of [{timeoutSeconds}s] exceeded while waiting for Azure Service Bus to relay the Message!");
        }
    }
}
