using System.Collections.Generic;
using System.Diagnostics;

namespace SqlTransactionalOutboxHelpers
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