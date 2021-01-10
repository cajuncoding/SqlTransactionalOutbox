using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace SqlTransactionalOutboxHelpers
{
    public class OutboxProcessingResults
    {
        public List<ISqlTransactionalOutboxItem> SuccessfullyPublishedItems { get; } = new List<ISqlTransactionalOutboxItem>();

        public List<ISqlTransactionalOutboxItem> FailedItems { get; } = new List<ISqlTransactionalOutboxItem>();

        public Stopwatch ProcessingTimer { get; set; }
    }
}
