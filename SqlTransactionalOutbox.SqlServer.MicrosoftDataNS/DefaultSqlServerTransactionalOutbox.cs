using System;
using Microsoft.Data.SqlClient;

namespace SqlTransactionalOutbox.SqlServer.MicrosoftDataNS
{
    public class DefaultSqlServerTransactionalOutbox<TPayload>: TransactionalOutbox<Guid, TPayload>
    {
        public DefaultSqlServerTransactionalOutbox(
            SqlTransaction sqlTransaction,
            int? distributedMutexAcquisitionTimeoutSeconds = null
        )
        : base (
            new DefaultSqlServerOutboxRepository<TPayload>(sqlTransaction, distributedMutexAcquisitionTimeoutSeconds: distributedMutexAcquisitionTimeoutSeconds)
        )
        {}
    }
}
