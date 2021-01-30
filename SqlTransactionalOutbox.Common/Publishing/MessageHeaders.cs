using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutbox.Publishing
{
    public class JsonMessageFields
    {
        public const string To = "to";
        public const string Body = "body";
        public const string FifoGroupingId= "fifoGroupingId";
        public const string SessionId = "sessionid";
        public const string CorrelationId = "correlationId";
        public const string ReplyTo = "replyTo";
        public const string ReplyToSessionId = "replyToSessionId";
        public const string PartitionKey = "partitionKey";
        public const string ContentType = "contentType";
        public const string Label = "label";
        public const string Headers = "headers";
        public const string UserProperties = "userProperties";

    }

    public class MessageHeaders
    {
        public static string CustomHeaderPrefix = "outbox";
        public static string CustomHeaderSeparator = "-";
        public static string ProcessorType = $"{CustomHeaderPrefix}-processor-type";
        public static string ProcessorSender = $"{CustomHeaderPrefix}-processor-sender";
        public static string OutboxUniqueIdentifier = $"{CustomHeaderPrefix}-item-unique-identifier";
        public static string OutboxCreatedDateUtc = $"{CustomHeaderPrefix}-item-created-date-utc";
        public static string OutboxPublishingAttempts = $"{CustomHeaderPrefix}-item-publishing-attempts";
        public static string OutboxPublishingTarget = $"{CustomHeaderPrefix}-item-publishing-target";

        public static string ToHeader(string name)
        {
            return string.Concat(MessageHeaders.CustomHeaderPrefix, MessageHeaders.CustomHeaderSeparator, name.ToLower());
        }
    }
}
