using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutbox.Publishing
{
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
