namespace SqlTransactionalOutbox
{
    public interface ISqlTransactionalOutboxInsertionItem<TPayload>
    {
        string PublishingTarget { get; set; }
        TPayload PublishingPayload { get; set; }
    }
}