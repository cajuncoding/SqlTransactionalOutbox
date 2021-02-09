using System;
using Microsoft.Data.SqlClient;
using SqlTransactionalOutbox.SqlServer.Common;

namespace SqlTransactionalOutbox.SqlServer.MicrosoftDataNS
{
    public class DefaultSqlServerTransactionalOutboxProcessor<TPayload> : TransactionalOutboxProcessor<Guid, TPayload>
    {
        public DefaultSqlServerTransactionalOutboxProcessor(
            SqlTransaction sqlTransaction,
            ISqlTransactionalOutboxPublisher<Guid> outboxPublisher,
            int distributedMutexAcquisitionTimeoutSeconds = Defaults.DistributedMutexAcquisitionTimeoutSeconds
        ) 
        : base (
            new DefaultSqlServerOutboxRepository<TPayload>(sqlTransaction, distributedMutexAcquisitionTimeoutSeconds: distributedMutexAcquisitionTimeoutSeconds), 
            outboxPublisher
        )
        {}
    }
}
