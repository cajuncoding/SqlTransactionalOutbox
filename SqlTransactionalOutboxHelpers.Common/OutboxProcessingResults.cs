using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace SqlTransactionalOutboxHelpers
{
    public class OutboxProcessingResults<TUniqueIdentifier> : ISqlTransactionalOutboxProcessingResults<TUniqueIdentifier>
    {
        public List<ISqlTransactionalOutboxItem<TUniqueIdentifier>> SuccessfullyPublishedItems { get; } 
            = new List<ISqlTransactionalOutboxItem<TUniqueIdentifier>>();

        public List<ISqlTransactionalOutboxItem<TUniqueIdentifier>> FailedItems { get; } 
            = new List<ISqlTransactionalOutboxItem<TUniqueIdentifier>>();

        public Stopwatch ProcessingTimer { get; set; }
    }
}
