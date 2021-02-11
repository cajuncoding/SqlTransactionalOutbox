using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace SqlTransactionalOutbox.Tests
{
    public class TestConfiguration
    {
        public static IConfigurationRoot ConfigurationRoot { get; }
        
        static TestConfiguration()
        {
            ConfigurationRoot = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            SqlConnectionString = ConfigurationRoot[nameof(SqlConnectionString)];
            AzureServiceBusConnectionString = ConfigurationRoot[nameof(AzureServiceBusConnectionString)];
        }

        public static string SqlConnectionString { get; }
        public static string AzureServiceBusConnectionString { get; }
    }
}
