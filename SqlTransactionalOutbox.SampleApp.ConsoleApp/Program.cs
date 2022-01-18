using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.SampleApp.AzureFunctions;
using SqlTransactionalOutbox.SampleApp.Common.Configuration;
using SqlTransactionalOutbox.SampleApp.ConsoleApp;
using SqlTransactionalOutbox.SqlServer.MicrosoftDataNS;

const string ServiceBusTopic = "SqlTransactionalOutbox/Integration-Tests";

Console.WriteLine("Starting Sql Transactional Outbox Demo...");

LocalSettingsEnvironmentReader.SetupEnvironmentFromLocalSettingsJson();

//We Need a Payload Sender to populate the Outbox with messages/payloads...
//  NOTE: this is a wrapper class to simplify this code and encapsulate Connection Handling!
var outboxSender = new OutboxSender(ServiceBusTopic, SampleAppConfig.SqlConnectionString);

//We Need a Publisher to publish Outbox Items...
//  NOTE: this is AsyncDisposable!
await using var outboxPublisher = OutboxFactory.CreateAzureServiceBusOutboxPublisher(
    SampleAppConfig.AzureServiceBusConnectionString,
    s => Console.WriteLine($"  LOG => {s}"),
    e => Console.WriteLine($"  ERROR => {e.GetMessagesRecursively()}")
);

//We Need Processing Agent (with associated Options) to process the Outbox on a background (Async) thread...
var outboxProcessingOptions = OutboxFactory.CreateOutboxProcessingOptions(
    SampleAppConfig.OutboxMaxPublishingRetryAttempts,
    SampleAppConfig.OutboxMaxTimeToLiveTimeSpan,
    s => Console.WriteLine($"  LOG => {s}"),
    e => Console.WriteLine($"  ERROR => {e.GetMessagesRecursively()}")
);

//  NOTE: this is AsyncDisposable!
await using var outboxProcessingAgent = new AspNetOutboxProcessingAgent(
    TimeSpan.FromSeconds(10),
    TimeSpan.FromDays(1),
    SampleAppConfig.SqlConnectionString,
    outboxPublisher,
    outboxProcessingOptions
);

//RUN The ProcessingAgent!
await outboxProcessingAgent.StartAsync();

while (true)
{
    Console.WriteLine("Enter Message?");
    string message = Console.ReadLine()?.Trim() ?? string.Empty;

    if (!string.IsNullOrWhiteSpace(message))
    {
        if (message?.Equals("exit", StringComparison.OrdinalIgnoreCase) == true)
            break;

        await outboxSender.SendMessageAsync(message);
        Console.WriteLine($"DELIVERED Message into Outbox: {message}");
    }
}

Console.WriteLine("Stopping Sql Transactional Outbox . . .");
await outboxProcessingAgent.StopAsync();

Console.WriteLine("Sql Transactional Outbox Demo Stopped!");
