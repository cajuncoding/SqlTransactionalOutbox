using System.Collections.Generic;
using System.Diagnostics;

namespace SqlTransactionalOutbox
{
    public interface ISqlTransactionalOutboxProcessingResults<TUniqueIdentifier>
    {
        List<ISqlTransactionalOutboxItem<TUniqueIdentifier>> SuccessfullyPublishedItems { get; }
        List<ISqlTransactionalOutboxItem<TUniqueIdentifier>> FailedItems { get; }
        List<ISqlTransactionalOutboxItem<TUniqueIdentifier>> SkippedItems { get; }

        int ProcessedItemsCount { get; }

        Stopwatch ProcessingTimer { get; }
    }
}