using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlTransactionalOutbox.IntegrationTests.ConfigurationTests;

namespace SqlTransactionalOutbox.IntegrationTests
{
    [TestClass]
    public class OutboxTableConfigTests
    {
        public TestContext TestContext { get; set; }

        private const string CustomSchemaName = "sqloutbox";

        [TestMethod]
        public void TestCustomImplementationOfOutboxTableConfigOverrides()
        {
            var defaultTableConfig = SqlTransactionalOutboxDefaults.OutboxTableConfig;
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

        [TestMethod]
        public void TestBuiltInOutboxTableConfigOverridesByConstructor()
        {
            var defaultTableConfig = SqlTransactionalOutboxDefaults.OutboxTableConfig;
            var customSchemaTableConfig = new OutboxTableConfig(transactionalOutboxSchemaName: CustomSchemaName);

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

        [TestMethod]
        public void TestBuiltInOutboxTableConfigOverridesByInitialization()
        {
            //GET DEFAULTS manually since we will override the Initialization defaults next...
            var defaultTableConfig = new OutboxTableConfig();
            
            //Now use the global initializer to change and read the custom value setting...
            SqlTransactionalOutboxInitializer.SetOutboxTableConfig(new OutboxTableConfig(transactionalOutboxSchemaName: CustomSchemaName));
            var customSchemaTableConfig = SqlTransactionalOutboxDefaults.OutboxTableConfig;

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
