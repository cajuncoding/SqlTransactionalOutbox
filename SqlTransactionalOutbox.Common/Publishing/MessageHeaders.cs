using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutbox.Publishing
{
    public class JsonMessageFields
    {
        public const string Topic = "topic"; // Convenience Alias for PublishTarget
        public const string PublishTopic = "publishTopic";
        public const string PublishTarget = "publishTarget";
        public const string QueueTarget = "queueTarget";

        public const string To = "to";
        public const string Body = "body";
        public const string FifoGroupingId = "fifoGroupingId"; // Convenience Alias for SessionId
        public const string SessionId = "sessionid";
        public const string CorrelationId = "correlationId";
        public const string ReplyTo = "replyTo";
        public const string ReplyToSessionId = "replyToSessionId";
        public const string PartitionKey = "partitionKey";
        public const string ContentType = "contentType";
        public const string Subject = "subject"; // Convenience Alias for Label
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

        private static readonly string ToHeaderPrefix = string.Concat(MessageHeaders.CustomHeaderPrefix, MessageHeaders.CustomHeaderSeparator);

        public static string ToHeader(string name)
        {
            var result = name.StartsWith(ToHeaderPrefix) ? name : string.Concat(ToHeaderPrefix, name.ToLower());
            return result;
        }
    }
}
