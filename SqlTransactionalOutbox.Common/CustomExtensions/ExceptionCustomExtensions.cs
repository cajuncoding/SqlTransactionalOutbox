using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutbox.CustomExtensions
{
    /// <summary>
    /// BBernard Custom Extensions for Exception Handling
    /// </summary>
    public static class SystemExceptionCustomExtensions
    {
        private const string _nestedExceptionFormatString = "[{0}] {1}";

        /// <summary>
        /// Retrieve a string containing ALL descendent Exception Messages using the specified format string; traversing all nested exceptions as necessary.
        /// </summary>
        /// <param name="thisException"></param>
        /// <returns></returns>
        public static string GetMessagesRecursively(this Exception thisException)
        {
            var stringBuilder = new StringBuilder();

            if (thisException != null)
            {
                var exceptionMessage = TerminateMessageHelper(thisException.Message);
                stringBuilder.AppendFormat(_nestedExceptionFormatString, thisException.GetType().Name, exceptionMessage);

                //Traverse all InnerExceptions
                var innerException = thisException.InnerException;
                while (innerException != null)
                {
                    exceptionMessage = TerminateMessageHelper(innerException.Message);
                    stringBuilder.AppendFormat(_nestedExceptionFormatString, innerException.GetType().Name, exceptionMessage);
                    innerException = innerException.InnerException;
                }

                //Handle New .Net 4.0 Aggregate Exception Type as a special Case because
                //AggregateExceptions contain a list of Exceptions thrown by background threads.
                if (thisException is AggregateException aggregateException)
                {
                    foreach (var exc in aggregateException.InnerExceptions)
                    {
                        exceptionMessage = TerminateMessageHelper(exc.Message);
                        stringBuilder.AppendFormat(_nestedExceptionFormatString, exc.GetType().Name, exceptionMessage);
                    }
                }
            }

            return stringBuilder.ToString();
        }

        private static string TerminateMessageHelper(string message)
        {
            if (!string.IsNullOrEmpty(message) && !message.EndsWith(".") && !message.EndsWith(";"))
            {
                return string.Concat(message, ";");
            }

            return message;
        }
    }
}
