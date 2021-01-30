using System;

namespace SqlTransactionalOutbox.SampleApp.AzureFunctions
{
    public class FunctionsConfiguration
    {
        static FunctionsConfiguration()
        {
            SqlConnectionString = Environment.GetEnvironmentVariable(nameof(SqlConnectionString));
            AzureServiceBusConnectionString = Environment.GetEnvironmentVariable(nameof(AzureServiceBusConnectionString));
        }

        public static string SqlConnectionString { get; }
        public static string AzureServiceBusConnectionString { get; }
    }
}
