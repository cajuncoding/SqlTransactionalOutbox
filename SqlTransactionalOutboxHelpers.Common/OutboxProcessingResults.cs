using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace SqlTransactionalOutboxHelpers
{
    public class OutboxProcessingResults
    {
        public List<OutboxItem> SuccessfullyPublishedItems { get; } = new List<OutboxItem>();

        public List<OutboxItem> FailedItems { get; } = new List<OutboxItem>();

        public Stopwatch ProcessingTimer { get; set; }
    }
}
