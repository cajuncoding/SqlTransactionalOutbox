using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlTransactionalOutbox;
using SqlTransactionalOutbox.IntegrationTests;
using SqlTransactionalOutbox.SqlServer.SystemDataNS;
using SqlTransactionalOutbox.Tests;
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
                    $"/publish/target_{(int)x % targetModulus}",
                    $"Payload Message #{x:00000}"
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

            Assert.AreEqual(leftItem.Status, rightItem.Status);
            Assert.AreEqual(leftItem.CreatedDateTimeUtc, rightItem.CreatedDateTimeUtc);
            Assert.AreEqual(leftItem.PublishAttempts, rightItem.PublishAttempts);
            Assert.AreEqual(leftItem.PublishTarget, rightItem.PublishTarget);
            Assert.AreEqual(leftItem.Payload, rightItem.Payload);
        }
    }
}
