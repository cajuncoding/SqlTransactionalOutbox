namespace SqlTransactionalOutbox
{
    public interface ISqlTransactionalOutboxInsertionItem<out TPayload>
    {
        string PublishingTarget { get; }
        TPayload PublishingPayload { get; }
        string FifoGroupingIdentifier { get; }
    }
}