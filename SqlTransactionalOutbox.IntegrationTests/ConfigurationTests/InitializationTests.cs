using System;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlTransactionalOutbox.IntegrationTests.ConfigurationTests;
using SqlTransactionalOutbox.SqlServer.MicrosoftDataNS;

namespace SqlTransactionalOutbox.IntegrationTests
{
    [TestClass]
    public class InitializationTests
    {
        public TestContext TestContext { get; set; }

        private const string CustomSchemaName = "sqloutbox";

        [TestInitialize]
        public void Initialize()
        {
            //Ensure that our Configuration matches the Default values which all tests to assume...
            SqlTransactionalOutboxInitializer.Configure(c => c.ResetToDefaults());
        }

        [TestCleanup]
        public void Cleanup()
        {
            //Ensure that our Configuration matches the Default values which all tests to assume...
            SqlTransactionalOutboxInitializer.Configure(c => c.ResetToDefaults());
        }

        [TestMethod]
        public void TestSqlTransactionalOutboxInitializationConfigurationBuilder()
        {
            //GET DEFAULTS manually since we will override the Initialization defaults next...
            var defaultTableConfig = new OutboxTableConfig();
            
            //Get initial Defaults for Distributed Mutex values...
            var defaultDistributedMutexLockTimeout = SqlTransactionalOutboxDefaults.DistributedMutexAcquisitionTimeoutSeconds;
            var defaultDistributedMutexPrefix = SqlTransactionalOutboxDefaults.DistributeMutexLockPrefix;

            //Now use the global initializer to change and read the custom value setting...
            SqlTransactionalOutboxInitializer.Configure(config =>
            {
                config.WithOutboxTableConfig(new OutboxTableConfig(
                        transactionalOutboxSchemaName: CustomSchemaName,
                        transactionalOutboxTableName: "OutboxTable",
                        pkeyFieldName: "PKeyField",
                        payloadFieldName: "PayloadField",
                        uniqueIdentifierFieldName: "UniqueIDField",
                        fifoGroupingIdentifier: "FifoField",
                        statusFieldName: "StatusField",
                        publishTargetFieldName: "PublishTargetField",
                        publishAttemptsFieldName: "PublishAttemptsField",
                        createdDateTimeUtcFieldName: "CreatedField"
                    ))
                    .WithDistributedMutexLockSettings(
                        lockAcquisitionTimeoutSeconds: 8,
                        lockNamePrefix: "OutboxDistributedLock::"
                    );
            });

            var customSchemaTableConfig = SqlTransactionalOutboxDefaults.OutboxTableConfig;

            //OutboxTableConfig settings...
            Assert.AreEqual(CustomSchemaName, customSchemaTableConfig.TransactionalOutboxSchemaName);
            Assert.AreEqual("OutboxTable", customSchemaTableConfig.TransactionalOutboxTableName);
            Assert.AreEqual("PKeyField", customSchemaTableConfig.PKeyFieldName);
            Assert.AreEqual("PayloadField", customSchemaTableConfig.PayloadFieldName);
            Assert.AreEqual("UniqueIDField", customSchemaTableConfig.UniqueIdentifierFieldName);
            Assert.AreEqual("FifoField", customSchemaTableConfig.FifoGroupingIdentifier);
            Assert.AreEqual("StatusField", customSchemaTableConfig.StatusFieldName);
            Assert.AreEqual("PublishTargetField", customSchemaTableConfig.PublishTargetFieldName);
            Assert.AreEqual("PublishAttemptsField", customSchemaTableConfig.PublishAttemptsFieldName);
            Assert.AreEqual("CreatedField", customSchemaTableConfig.CreatedDateTimeUtcFieldName);

            Assert.AreNotEqual(defaultTableConfig.TransactionalOutboxSchemaName, customSchemaTableConfig.TransactionalOutboxSchemaName);
            Assert.AreNotEqual(defaultTableConfig.TransactionalOutboxTableName, customSchemaTableConfig.TransactionalOutboxTableName);
            Assert.AreNotEqual(defaultTableConfig.PKeyFieldName, customSchemaTableConfig.PKeyFieldName);
            Assert.AreNotEqual(defaultTableConfig.PayloadFieldName, customSchemaTableConfig.PayloadFieldName);
            Assert.AreNotEqual(defaultTableConfig.UniqueIdentifierFieldName, customSchemaTableConfig.UniqueIdentifierFieldName);
            Assert.AreNotEqual(defaultTableConfig.FifoGroupingIdentifier, customSchemaTableConfig.FifoGroupingIdentifier);
            Assert.AreNotEqual(defaultTableConfig.StatusFieldName, customSchemaTableConfig.StatusFieldName);
            Assert.AreNotEqual(defaultTableConfig.PublishTargetFieldName, customSchemaTableConfig.PublishTargetFieldName);
            Assert.AreNotEqual(defaultTableConfig.PublishAttemptsFieldName, customSchemaTableConfig.PublishAttemptsFieldName);
            Assert.AreNotEqual(defaultTableConfig.CreatedDateTimeUtcFieldName, customSchemaTableConfig.CreatedDateTimeUtcFieldName);

            Assert.AreEqual(OutboxTableConfig.DefaultTransactionalOutboxSchemaName, defaultTableConfig.TransactionalOutboxSchemaName);

            //Distributed Mutex Lock settings...
            Assert.AreEqual(8, SqlTransactionalOutboxDefaults.DistributedMutexAcquisitionTimeoutSeconds);
            Assert.AreEqual("OutboxDistributedLock::", SqlTransactionalOutboxDefaults.DistributeMutexLockPrefix);
            Assert.AreNotEqual(defaultDistributedMutexLockTimeout, SqlTransactionalOutboxDefaults.DistributedMutexAcquisitionTimeoutSeconds);
            Assert.AreNotEqual(defaultDistributedMutexPrefix, SqlTransactionalOutboxDefaults.DistributeMutexLockPrefix);


            //Validate Interface Cast references
            var defaultTableConfigAsInterface = (ISqlTransactionalOutboxTableConfig)defaultTableConfig;
            var customOutboxTableConfigAsInterface = (ISqlTransactionalOutboxTableConfig)customSchemaTableConfig;
            Assert.AreEqual(CustomSchemaName, customOutboxTableConfigAsInterface.TransactionalOutboxSchemaName);
            Assert.AreEqual(OutboxTableConfig.DefaultTransactionalOutboxSchemaName, defaultTableConfigAsInterface.TransactionalOutboxSchemaName);
            Assert.AreNotEqual(defaultTableConfigAsInterface.TransactionalOutboxSchemaName, customOutboxTableConfigAsInterface.TransactionalOutboxSchemaName);
        }
    }
}
