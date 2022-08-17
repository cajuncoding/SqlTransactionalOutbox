using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlTransactionalOutbox.IntegrationTests.ConfigurationTests
{
    public class CustomSchemaNameOutboxTableConfig : OutboxTableConfig, ISqlTransactionalOutboxTableConfig
    {
        public CustomSchemaNameOutboxTableConfig(string customOutboxSchemaName)
        {
            TransactionalOutboxSchemaName = customOutboxSchemaName;
        }

        public new string TransactionalOutboxSchemaName { get; }
    }
}
