using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlTransactionalOutbox.IntegrationTests.ConfigurationTests;

namespace SqlTransactionalOutbox.IntegrationTests
{
    [TestClass]
    public class OutboxTableConfigTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestCustomOutboxTableConfigOverrides()
        {
            const string CustomSchemaName = "sqloutbox";
            var defaultTableConfig = new OutboxTableConfig();
            var customSchemaTableConfig = new CustomSchemaNameOutboxTableConfig(CustomSchemaName);

            //Validate Class references
            Assert.AreEqual(CustomSchemaName, customSchemaTableConfig.TransactionalOutboxSchemaName);
            Assert.AreEqual(OutboxTableConfig.DefaultTransactionalOutboxSchemaName, defaultTableConfig.TransactionalOutboxSchemaName);
            Assert.AreNotEqual(defaultTableConfig.TransactionalOutboxSchemaName, customSchemaTableConfig.TransactionalOutboxSchemaName);
            Assert.AreEqual(defaultTableConfig.TransactionalOutboxTableName, customSchemaTableConfig.TransactionalOutboxTableName);

            //Validate Interface Cast references

            var defaultTableConfigAsInterface = (ISqlTransactionalOutboxTableConfig)defaultTableConfig;
            var customOutboxTableConfigAsInterface = (ISqlTransactionalOutboxTableConfig)customSchemaTableConfig;
            Assert.AreEqual(CustomSchemaName, customOutboxTableConfigAsInterface.TransactionalOutboxSchemaName);
            Assert.AreEqual(OutboxTableConfig.DefaultTransactionalOutboxSchemaName, defaultTableConfigAsInterface.TransactionalOutboxSchemaName);
            Assert.AreNotEqual(defaultTableConfigAsInterface.TransactionalOutboxSchemaName, customOutboxTableConfigAsInterface.TransactionalOutboxSchemaName);
            Assert.AreEqual(defaultTableConfigAsInterface.TransactionalOutboxTableName, customOutboxTableConfigAsInterface.TransactionalOutboxTableName);
        }
    }
}
