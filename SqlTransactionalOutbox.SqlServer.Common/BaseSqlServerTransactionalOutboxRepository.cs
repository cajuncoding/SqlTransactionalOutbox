using System;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox.SqlServer.Common
{
    public abstract class BaseSqlServerTransactionalOutboxRepository<TUniqueIdentifier, TPayload>
    {
        protected ISqlTransactionalOutboxTableConfig OutboxTableConfig { get; set; }
        protected SqlServerTransactionalOutboxQueryBuilder<TUniqueIdentifier> QueryBuilder { get; set; }
        protected ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> OutboxItemFactory { get; set; }
        protected int DistributedMutexAcquisitionTimeoutSeconds { get; set; }
        protected string DistributedMutexLockName { get; set; }

        protected void Init(
            ISqlTransactionalOutboxTableConfig outboxTableConfig,
            ISqlTransactionalOutboxItemFactory<TUniqueIdentifier, TPayload> outboxItemFactory,
            int distributedMutexAcquisitionTimeoutSeconds
        )
        {
            //Possible Dependencies
            OutboxTableConfig = outboxTableConfig.AssertNotNull(nameof(outboxTableConfig));
            OutboxItemFactory = outboxItemFactory.AssertNotNull(nameof(outboxItemFactory));

            //Default Known setup for Sql Server...
            QueryBuilder = new SqlServerTransactionalOutboxQueryBuilder<TUniqueIdentifier>(outboxTableConfig); 
            DistributedMutexLockName = $"{SqlTransactionalOutboxDefaults.DistributeMutexLockPrefix}{QueryBuilder.BuildTableName()}";
            DistributedMutexAcquisitionTimeoutSeconds = distributedMutexAcquisitionTimeoutSeconds;
        }


    }
}
