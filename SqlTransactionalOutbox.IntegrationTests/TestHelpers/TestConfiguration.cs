using System;
using SqlTransactionalOutbox.SampleApp.AzureFunctions;
using SqlTransactionalOutbox.SampleApp.Common.Configuration;

namespace SqlTransactionalOutbox.Tests
{
    public class TestConfiguration
    {
        public static SampleAppConfig SettingsConfig { get; }
        
        static TestConfiguration()
        {
            LocalSettingsEnvironmentReader.SetupEnvironmentFromLocalSettingsJson();
            SettingsConfig = new SampleAppConfig();
        }

        public static string SqlConnectionString => SettingsConfig.SqlConnectionString;
        public static string AzureServiceBusConnectionString => SettingsConfig.AzureServiceBusConnectionString;

        public static string AzureServiceBusTopic => SettingsConfig.AzureServiceBusTopic;
        public static string AzureServiceBusSubscription => SettingsConfig.AzureServiceBusSubscription;
    }
}
