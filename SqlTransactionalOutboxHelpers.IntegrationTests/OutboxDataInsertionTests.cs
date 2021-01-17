using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlTransactionalOutboxHelpers.SqlServer.SystemDataNS;
using SqlTransactionalOutboxHelpers.Tests;

namespace SqlTransactionalOutboxHelpers.IntegrationTests
{
    [TestClass]
    public class OutboxDataInsertionTests
    {
        [TestMethod]
        public async Task TestNewOutboxItemInsertionWithModerateDataSet()
        {
            
            await using var sqlConnection = SqlConnectionHelper.CreateSystemDataSqlConnection();
            await using var sqlTransaction = (SqlTransaction)await sqlConnection.BeginTransactionAsync().ConfigureAwait(false);

            var noOpPublisher = new NoOpSqlTransactionalOutboxPublisher();
            var outboxProcessor = new SqlServerTransactionalOutboxProcessor<string>(sqlTransaction, noOpPublisher);

            var outboxTestItems = TestHelper.CreateTestStringOutboxItemData(100);
            var insertedResults = await outboxProcessor.InsertNewPendingOutboxItemsAsync(outboxTestItems).ConfigureAwait(false);

            var utcNow = DateTime.UtcNow;
            foreach (var result in insertedResults)
            {
                //Validate Created Date Time (can't match precisely but can validate it was populated as expected...
                Assert.AreEqual(result.CreatedDateTimeUtc.Date, utcNow.Date);
                Assert.AreEqual(result.CreatedDateTimeUtc.Hour, utcNow.Hour);
                Assert.AreEqual(result.CreatedDateTimeUtc.Minute, utcNow.Minute);
            }
        }
    }
}
