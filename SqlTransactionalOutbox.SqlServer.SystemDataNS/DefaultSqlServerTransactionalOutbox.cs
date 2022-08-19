using System;
using System.Data.SqlClient;
using SqlTransactionalOutbox.SqlServer.Common;

namespace SqlTransactionalOutbox.SqlServer.SystemDataNS
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
