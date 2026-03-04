#nullable disable

using System.Diagnostics;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox.SampleApp.ConsoleApp
{
    public static class OutboxHelpers
    {
        public static Action<string> DefaultLogDebugCallback = (s) => Debug.WriteLine(s);
        public static Action<Exception> DefaultLogErrorCallback = (e) => Debug.WriteLine(e.GetMessagesRecursively(), "Unexpected Exception occurred while Processing the Transactional Outbox.");
    }
}
