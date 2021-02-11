using System;
using Microsoft.Data.SqlClient;
using SqlTransactionalOutbox.SqlServer.Common;

namespace SqlTransactionalOutbox.SqlServer.MicrosoftDataNS
{
    public class DefaultSqlServerTransactionalOutbox<TPayload>: TransactionalOutbox<Guid, TPayload>
    {
        public DefaultSqlServerTransactionalOutbox(
            SqlTransaction sqlTransaction,
            int distributedMutexAcquisitionTimeoutSeconds = Defaults.DistributedMutexAcquisitionTimeoutSeconds
        )
        : base (
            new DefaultSqlServerOutboxRepository<TPayload>(sqlTransaction, distributedMutexAcquisitionTimeoutSeconds: distributedMutexAcquisitionTimeoutSeconds)
        )
        {}
    }
}
