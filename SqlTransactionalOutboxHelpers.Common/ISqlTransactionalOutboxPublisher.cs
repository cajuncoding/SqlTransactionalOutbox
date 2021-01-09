using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SqlTransactionalOutboxHelpers
{
    public interface ISqlTransactionalOutboxPublisher
    {
        Task PublishOutboxItemAsync(OutboxItem outboxItem);
    }
}
