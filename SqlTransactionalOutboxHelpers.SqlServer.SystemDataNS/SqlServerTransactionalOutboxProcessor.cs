using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;

namespace SqlTransactionalOutboxHelpers.SqlServer.SystemDataNS
{
    public class SqlServerTransactionalOutboxProcessor : BaseSqlServerTransactionalOutboxProcessor, ISqlTransactionalOutboxProcessor
    {
        public SqlServerTransactionalOutboxProcessor(
            SqlTransaction sqlTransaction,
            ISqlTransactionalOutboxPublisher outboxPublisher
        )
        {
            //Initialize Sql Server repository and Outbox Processor with needed dependencies
            var sqlServerOutboxRepository = new SqlServerTransactionalOutboxRepository(sqlTransaction);
            base.Init(new OutboxProcessor(sqlServerOutboxRepository, outboxPublisher));
        }
    }
}
