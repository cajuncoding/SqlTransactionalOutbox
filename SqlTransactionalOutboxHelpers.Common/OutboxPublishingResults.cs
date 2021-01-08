using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutboxHelpers
{
    public class OutboxPublishingResults
    {
        public List<OutboxItem> SuccessfullyPublishedItems { get; } = new List<OutboxItem>();

        public List<OutboxItem> FailedItems { get; } = new List<OutboxItem>();
    }
}
