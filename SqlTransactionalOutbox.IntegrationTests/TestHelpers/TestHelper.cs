using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlTransactionalOutbox;
using SqlTransactionalOutbox.IntegrationTests;
using SqlTransactionalOutbox.Receiving;
using SqlTransactionalOutbox.SqlServer.SystemDataNS;
using SqlTransactionalOutbox.Tests;
using SqlTransactionalOutbox.Utilities;
using SystemData = System.Data.SqlClient;
//using MicrosoftData = Microsoft.Data.SqlClient;

namespace SqlTransactionalOutbox.Tests
{
    public class TestHelper
    {
        public static List<OutboxInsertionItem<string>> CreateTestStringOutboxItemData(int dataSize, int targetModulus = 5)
        {
            var list = new List<OutboxInsertionItem<string>>();
            for (var x = 1; x <= dataSize; x++)
            {
                list.Add(new OutboxInsertionItem<string>(
                    publishingTarget: $"/publish/target_{(int)x % targetModulus}",
                    publishingPayload: $"Payload Message #{x:00000}",
                    fifoGroupingIdentifier: $"IntegrationTests:{nameof(CreateTestStringOutboxItemData)}"
                ));
            }

            return list;
        }

        public static void ForEach(List<ISqlTransactionalOutboxItem<Guid>> itemsList, Action<ISqlTransactionalOutboxItem<Guid>> matchAction)
        {
            foreach (var item in itemsList)
            {
                matchAction.Invoke(item);
            }
        }

        public static void AssertOutboxItemResultsMatch(
            List<ISqlTransactionalOutboxItem<Guid>> leftResults,
            List<ISqlTransactionalOutboxItem<Guid>> rightResults
        )
        {
            Assert.AreEqual(leftResults.Count, leftResults.Count);

            var leftItemsLookup = leftResults.ToLookup(i => i.UniqueIdentifier);
            foreach (var rightItem in rightResults)
            {
                var leftItem = leftItemsLookup[rightItem.UniqueIdentifier].FirstOrDefault();
                AssertOutboxItemsMatch(leftItem, rightItem);
            }
        }

        public static void AssertOutboxItemsMatch(
            ISqlTransactionalOutboxItem<Guid> leftItem,
            ISqlTransactionalOutboxItem<Guid> rightItem 
        )
        {
            Assert.IsNotNull(leftItem);
            Assert.IsNotNull(rightItem);

            Assert.AreEqual(leftItem.UniqueIdentifier, rightItem.UniqueIdentifier);
            Assert.AreEqual(leftItem.FifoGroupingIdentifier, rightItem.FifoGroupingIdentifier);
            Assert.AreEqual(leftItem.PublishTarget, rightItem.PublishTarget);
            Assert.AreEqual(leftItem.Payload, rightItem.Payload);
            Assert.AreEqual(leftItem.Status, rightItem.Status);
            Assert.AreEqual(leftItem.PublishAttempts, rightItem.PublishAttempts);
            //Dates need to be compared at the Millisecond level due to minute differences in Ticks when re-parsing(?)...
            Assert.AreEqual(0,
                (int)(leftItem.CreatedDateTimeUtc - rightItem.CreatedDateTimeUtc).TotalMilliseconds,
                "The Created Dates differ at the Millisecond level"
            );
        }


        public static void AssertOutboxItemMatchesReceivedItem(
            ISqlTransactionalOutboxItem<Guid> outboxItem,
            ISqlTransactionalOutboxReceivedItem<Guid, string> receivedItem
        )
        {
            Assert.IsNotNull(outboxItem);
            Assert.IsNotNull(receivedItem);

            Assert.AreEqual(outboxItem.UniqueIdentifier, receivedItem.UniqueIdentifier);
            Assert.AreEqual(outboxItem.FifoGroupingIdentifier, receivedItem.FifoGroupingIdentifier);
            Assert.AreEqual(OutboxReceivedItemProcessingStatus.AcknowledgeSuccessfulReceipt, receivedItem.Status);

            var publishedItem = receivedItem.PublishedItem;
            //Received Items should always be Successful
            Assert.AreEqual(OutboxItemStatus.Successful, publishedItem.Status);
            Assert.AreEqual(outboxItem.UniqueIdentifier, publishedItem.UniqueIdentifier);
            Assert.AreEqual(outboxItem.FifoGroupingIdentifier, publishedItem.FifoGroupingIdentifier);
            Assert.AreEqual(outboxItem.PublishTarget, publishedItem.PublishTarget);
            //Publishing process should increment the PublishAttempt by 1
            Assert.AreEqual(outboxItem.PublishAttempts + 1, publishedItem.PublishAttempts);

            //Extract the Body from the original OutboxItem to compare (apples to apples) to the received item from
            //  being published where the Payload would be automatically populated from the Body property mapping.
            var payloadBuilder = PayloadBuilder.FromJsonSafely(outboxItem.Payload);
            Assert.AreEqual(payloadBuilder.Body, publishedItem.Payload);

            //Dates need to be compared at the Millisecond level due to minute differences in Ticks when re-parsing(?)...
            Assert.AreEqual(0, 
                (int)(publishedItem.CreatedDateTimeUtc - outboxItem.CreatedDateTimeUtc).TotalMilliseconds,
                "The Created Dates differ at the Millisecond level"
            );
        }
    }
}
