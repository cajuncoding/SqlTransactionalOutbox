using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;

namespace SqlTransactionalOutbox.SqlServer.SystemDataNS
{
    public class SqlServerGuidTransactionalOutboxRepository<TPayload> : SqlServerGenericsTransactionalOutboxRepository<Guid, TPayload>
    {
        public SqlServerGuidTransactionalOutboxRepository(
            SqlTransaction sqlTransaction,
            ISqlTransactionalOutboxTableConfig outboxTableConfig = null,
            ISqlTransactionalOutboxItemFactory<Guid, TPayload> outboxItemFactory = null,
            int distributedMutexAcquisitionTimeoutSeconds = 5
        ) : base (
            sqlTransaction: sqlTransaction,
            outboxTableConfig: outboxTableConfig ?? new DefaultOutboxTableConfig(),
            outboxItemFactory: outboxItemFactory ?? new OutboxItemFactory<Guid, TPayload>(
                                   uniqueIdFactory: new OutboxItemUniqueIdentifierGuidFactory(),
                                   payloadSerializer: new OutboxPayloadJsonSerializer()
                               ), 
            distributedMutexAcquisitionTimeoutSeconds
        )
        {
            //Preconfigured defaults are used in base constructor above...
        }
    }
}
