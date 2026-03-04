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
