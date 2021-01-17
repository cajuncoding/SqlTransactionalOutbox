namespace SqlTransactionalOutboxHelpers
{
    public interface ISqlTransactionalOutboxInsertionItem<TPayload>
    {
        string PublishingTarget { get; set; }
        TPayload PublishingPayload { get; set; }
    }
}