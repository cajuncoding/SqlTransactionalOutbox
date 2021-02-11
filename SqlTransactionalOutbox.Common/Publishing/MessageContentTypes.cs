using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutbox
{
    public class MessageContentTypes
    {
        public const string Json = "application/json;charset=utf-8";
        public const string Xml = "text/xml;charset=utf-8";
        public const string PlainText = "text/plain;charset=utf-8";
    }
}
