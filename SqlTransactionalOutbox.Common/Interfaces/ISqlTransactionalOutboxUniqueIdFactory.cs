namespace SqlTransactionalOutbox
{
    public interface ISqlTransactionalOutboxUniqueIdFactory<out TUniqueIdentifier>
    {
        TUniqueIdentifier CreateUniqueIdentifier();
        TUniqueIdentifier ParseUniqueIdentifier(string uniqueIdentifier);
    }
}
