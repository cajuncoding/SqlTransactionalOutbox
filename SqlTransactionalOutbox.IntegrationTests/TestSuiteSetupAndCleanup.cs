using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlTransactionalOutbox.Tests;
using SystemTextJsonHelpers;

namespace SqlTransactionalOutbox.IntegrationTests
{
    [TestClass] //REQUIRED FOR Events to be registered!
    public class TestSuiteSetupAndCleanup
    {
        [AssemblyInitialize]
        public static async Task AssemblyInitializeAsync(TestContext context)
        {
            SystemTextJsonDefaults.ConfigureRelaxedWebDefaults();
            await CleanupOutboxAsync();
        }

        [AssemblyCleanup]
        public static async Task AssemblyCleanupAsync()
        {
            await CleanupOutboxAsync();
        }

        protected static async Task CleanupOutboxAsync()
        {
            await using var sqlConnection = await SqlConnectionHelper.CreateMicrosoftDataSqlConnectionAsync();
            await sqlConnection.TruncateTransactionalOutboxTableAsync();
        }
    }
}
