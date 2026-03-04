namespace SqlTransactionalOutbox.IntegrationTests.ConfigurationTests
{
    public class CustomSchemaNameOutboxTableConfig : OutboxTableConfig, ISqlTransactionalOutboxTableConfig
    {
        public CustomSchemaNameOutboxTableConfig(string customOutboxSchemaName)
        {
            TransactionalOutboxSchemaName = customOutboxSchemaName;
        }

        public override string TransactionalOutboxSchemaName { get; protected set; }
    }
}
