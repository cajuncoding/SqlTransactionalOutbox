namespace SqlTransactionalOutbox
{
    public interface ISqlTransactionalOutboxSerializer
    {
        string SerializePayload<TPayload>(TPayload payload);
        TPayload DeserializePayload<TPayload>(string serializedPayload);
    }
}
