using SqlTransactionalOutbox.AzureServiceBus;
using SqlTransactionalOutbox.CustomExtensions;
using SqlTransactionalOutbox.SampleApp.AzureFunctions;
using SqlTransactionalOutbox.SampleApp.Common.Configuration;
using SqlTransactionalOutbox.SampleApp.ConsoleApp;
using SqlTransactionalOutbox.SqlServer.MicrosoftDataNS;

string enterMessageDescription = Environment.NewLine + "Enter Message?  (or 'Exit' to stop)";

Console.WriteLine("Starting Sql Transactional Outbox Demo...");

LocalSettingsEnvironmentReader.SetupEnvironmentFromLocalSettingsJson();
var configSettings = new SampleAppConfig();
//******************************************************************************************
// 1. SENDING Messages via the Sql Transactional Outbox
//  We Need a Payload Sender to populate the Outbox with messages/payloads...
//******************************************************************************************
var outboxSender = new OutboxSender(configSettings);

//******************************************************************************************
// 2. PROCESSING & PUBLISHING Messages in the Sql Transactional Outbox to Azure Service Bus
//  We Need a fully initialized Processing Agent to Process the Outbox on an Async Thread!
//******************************************************************************************
//  NOTE: this is AsyncDisposable!
await using var outboxProcessor = new OutboxProcessor(configSettings);
await outboxProcessor.StartProcessingAsync();

//******************************************************************************************
// 3. RECEIVING Messages that were Published via AzureServiceBus
//  Finally we need a Message Receiver to pickup the Messages that are Published!
//******************************************************************************************
// NOTE: Since our Subscription is Session based we must enable Fifo processing or errors will occur!
// NOTE: Since our payloads are simple strings we use 'string' payload type!
var messageReceiver = new OutboxFifoReceiver<string>(configSettings);
await messageReceiver.StartReceivingWithAsync(s =>
{
    Console.WriteLine(s);
    //Once we output an Item re-prompt the user for better UI...
    Console.WriteLine(enterMessageDescription);
});

//******************************************************************************************
// 4. FINALLY Implement our Console Based UI...
//******************************************************************************************
while (true)
{
    Console.WriteLine(enterMessageDescription);
    var message = Console.ReadLine()?.Trim() ?? string.Empty;

    if (!string.IsNullOrWhiteSpace(message))
    {
        if (message.Equals("exit", StringComparison.OrdinalIgnoreCase))
            break;

        await outboxSender.SendMessageAsync(message);
        Console.WriteLine($"[{nameof(OutboxSender)}] Successfully Delivered Message into Outbox: {message}");
    }
}

Console.WriteLine("Stopping Sql Transactional Outbox . . .");
//NOTE: This is not completely necessary as these will be stopped when Disposed of...
var processingExecutionCount = await outboxProcessor.StopProcessingAsync();
await messageReceiver.StopReceivingAsync();

Console.WriteLine($"Sql Transactional Outbox Demo Stopped after Processing the Sql Transactional Outbox [{processingExecutionCount}] times!");
Console.WriteLine($"{Environment.NewLine}Press any key to end...");
Console.ReadKey();
