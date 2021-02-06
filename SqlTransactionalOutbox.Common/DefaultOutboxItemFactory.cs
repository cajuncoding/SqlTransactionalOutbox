using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutbox
{
    public class DefaultOutboxItemFactory<TPayload> : OutboxItemFactory<Guid, TPayload>
    {
        public DefaultOutboxItemFactory(
            ISqlTransactionalOutboxSerializer payloadSerializer = null
        ) 
        : base(
            new OutboxGuidUniqueIdentifier(),
            payloadSerializer ?? new OutboxPayloadJsonSerializer()
        )
        {
        }
    }
}
