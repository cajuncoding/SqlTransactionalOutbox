#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlTransactionalOutbox.AzureServiceBus;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.SqlServer.MicrosoftDataNS;

namespace SqlTransactionalOutbox.SampleApp.ConsoleApp
{
    public static class OutboxHelpers
    {
        public static Action<string> DefaultLogDebugCallback = (s) => Debug.WriteLine(s);
        public static Action<Exception> DefaultLogErrorCallback = (e) => Debug.WriteLine(e.GetMessagesRecursively(), "Unexpected Exception occurred while Processing the Transactional Outbox.");
    }
}
