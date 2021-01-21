using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace SqlTransactionalOutbox
{
    public class OutboxProcessingResults<TUniqueIdentifier> : ISqlTransactionalOutboxProcessingResults<TUniqueIdentifier>
    {
        public List<ISqlTransactionalOutboxItem<TUniqueIdentifier>> SuccessfullyPublishedItems { get; } 
            = new List<ISqlTransactionalOutboxItem<TUniqueIdentifier>>();

        public List<ISqlTransactionalOutboxItem<TUniqueIdentifier>> FailedItems { get; } 
            = new List<ISqlTransactionalOutboxItem<TUniqueIdentifier>>();

        public List<ISqlTransactionalOutboxItem<TUniqueIdentifier>> SkippedItems { get; }
            = new List<ISqlTransactionalOutboxItem<TUniqueIdentifier>>();

        public int ProcessedItemsCount => SuccessfullyPublishedItems.Count + FailedItems.Count;

        public List<ISqlTransactionalOutboxItem<TUniqueIdentifier>> GetAllProcessedItems(bool includeSuccessfulItems = true, bool includeFailedItems = true)
        {
            var processedItems = new List<ISqlTransactionalOutboxItem<TUniqueIdentifier>>();
            if(includeSuccessfulItems)
                processedItems.AddRange(this.SuccessfullyPublishedItems);

            if(includeFailedItems)
                processedItems.AddRange(this.FailedItems);

            return processedItems;
        }

        public Stopwatch ProcessingTimer { get; protected set; } = new Stopwatch();
    }
}
