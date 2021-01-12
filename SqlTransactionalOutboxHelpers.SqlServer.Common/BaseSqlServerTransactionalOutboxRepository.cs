using System;
using System.Linq;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using SqlTransactionalOutboxHelpers.CustomExtensions;

namespace SqlTransactionalOutboxHelpers.SqlServer.SystemDataNS
{
    public abstract class BaseSqlServerTransactionalOutboxRepository
    {
        protected ISqlTransactionalOutboxTableConfig OutboxTableConfig { get; set; }
        protected SqlServerTransactionalOutboxQueryBuilder QueryBuilder { get; set; }
        protected ISqlTransactionalOutboxItemFactory OutboxItemFactory { get; set; }
        protected int DistributedMutexAcquisitionTimeoutSeconds { get; set; }
        protected string DistributedMutexLockName { get; set; }

        protected void Init(
            ISqlTransactionalOutboxTableConfig outboxTableConfig = null,
            ISqlTransactionalOutboxItemFactory outboxItemFactory = null,
            int distributedMutexAcquisitionTimeoutSeconds = 5
        )
        {
            OutboxTableConfig = outboxTableConfig.AssertNotNull(nameof(outboxTableConfig));
            QueryBuilder = new SqlServerTransactionalOutboxQueryBuilder(outboxTableConfig);
            OutboxItemFactory = outboxItemFactory ?? new OutboxItemFactory();
            DistributedMutexLockName = $"SqlServerTransactionalOutboxProcessor::{QueryBuilder.BuildTableName()}";
            DistributedMutexAcquisitionTimeoutSeconds = distributedMutexAcquisitionTimeoutSeconds;
        }


    }
}
