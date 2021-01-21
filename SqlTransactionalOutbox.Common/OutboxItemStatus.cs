using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutbox
{
    public enum OutboxItemStatus
    {
        Pending,
        Successful,
        FailedAttemptsExceeded,
        FailedExpired
    }
}
