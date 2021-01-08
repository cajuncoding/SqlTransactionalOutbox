using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutboxHelpers
{
    public enum OutboxItemStatus
    {
        Pending,
        Successful,
        FailedAttemptsExceeded,
        FailedExpired
    }
}
