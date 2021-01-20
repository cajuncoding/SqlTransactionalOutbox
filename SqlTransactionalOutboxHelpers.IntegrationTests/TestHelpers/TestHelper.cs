using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlTransactionalOutboxHelpers;
using SqlTransactionalOutboxHelpers.IntegrationTests;
using SqlTransactionalOutboxHelpers.SqlServer.SystemDataNS;
using SqlTransactionalOutboxHelpers.Tests;
using SystemData = System.Data.SqlClient;
//using MicrosoftData = Microsoft.Data.SqlClient;

namespace SqlTransactionalOutboxHelpers.Tests
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
                Assert.IsNotNull(leftItem);

                Assert.AreEqual(leftItem.Status, rightItem.Status);
                Assert.AreEqual(leftItem.CreatedDateTimeUtc, rightItem.CreatedDateTimeUtc);
                Assert.AreEqual(leftItem.PublishingAttempts, rightItem.PublishingAttempts);
                Assert.AreEqual(leftItem.PublishingTarget, rightItem.PublishingTarget);
                Assert.AreEqual(leftItem.PublishingPayload, rightItem.PublishingPayload);
            }
        }
    }
}
