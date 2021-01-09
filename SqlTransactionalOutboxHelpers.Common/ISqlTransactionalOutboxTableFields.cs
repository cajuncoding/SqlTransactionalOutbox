﻿using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutboxHelpers
{
    public interface ISqlTransactionalOutboxTableFields
    {
        string UUIDFieldName { get; }
        
        string StatusFieldName { get; }
        
        string PublishingTargetFieldName { get; }
        
        string PublishingPayloadFieldName { get; }

        string PublishingAttemptsFieldName { get; }

        string CreatedDateTimeUtcFieldName { get; }
        
        string ExpirationDateTimeUtcFieldName { get; }
    }
}
