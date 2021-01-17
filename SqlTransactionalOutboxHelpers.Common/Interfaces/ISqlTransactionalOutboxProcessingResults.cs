using System.Collections.Generic;
using System.Diagnostics;

namespace SqlTransactionalOutboxHelpers
{
    public interface ISqlTransactionalOutboxProcessingResults<TUniqueIdentifier>
    {
        List<ISqlTransactionalOutboxItem<TUniqueIdentifier>> SuccessfullyPublishedItems { get; }
        List<ISqlTransactionalOutboxItem<TUniqueIdentifier>> FailedItems { get; }
        Stopwatch ProcessingTimer { get; set; }
    }
}