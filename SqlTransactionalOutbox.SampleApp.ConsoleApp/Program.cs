using SqlTransactionalOutbox.SampleApp.AzureFunctions;
using SqlTransactionalOutbox.SampleApp.Common.Configuration;
using SqlTransactionalOutbox.SampleApp.ConsoleApp;
using SqlTransactionalOutbox.CustomExtensions;

string enterMessageDescription = $"Enter Message to Send via Azure Service Bus?  (or 'Exit' to stop)";

static string ReadLineSafely() => Console.ReadLine()?.Trim() ?? string.Empty;
static void WriteBlankLine() => Console.WriteLine(string.Empty);
static void WriteLine(string message) => Console.WriteLine($"[{DateTime.Now}] {message}");

WriteLine("Starting Sql Transactional Outbox Demo...");

LocalSettingsEnvironmentReader.SetupEnvironmentFromLocalSettingsJson();
var appConfig = new SampleAppConfig();

//******************************************************************************************
// 1. SENDING Messages via the Sql Transactional Outbox
//  We Need a Payload Sender to populate the Outbox with messages/payloads...
//******************************************************************************************
var outboxSender = new OutboxSender(appConfig);

//******************************************************************************************
// 2. PROCESSING & PUBLISHING Messages in the Sql Transactional Outbox to Azure Service Bus
//  We Need a fully initialized Processing Agent to Process the Outbox on an Async Thread!
//******************************************************************************************
//  NOTE: this is AsyncDisposable!
await using var outboxProcessor = new OutboxProcessor(appConfig);
await outboxProcessor.StartProcessingAsync();

//******************************************************************************************
// 3. RECEIVING Messages that were Published via AzureServiceBus
//  Finally we need a Message Receiver to pickup the Messages that are Published!
//******************************************************************************************
// NOTE: Since our Subscription is Session based we must enable Fifo processing or errors will occur!
// NOTE: Since our payloads are simple strings we use 'string' payload type!
var messageReceiver = new OutboxFifoReceiver<string>(appConfig);
await messageReceiver.StartReceivingWithAsync(s =>
{
    WriteBlankLine();
    WriteLine(s);
    //Once we output an Item re-prompt the user for iterative/continuing UX...
    WriteBlankLine();
    WriteLine(enterMessageDescription);
});

//******************************************************************************************
// 4. FINALLY Implement our Console Based UI...
//******************************************************************************************
while (true)
{
    WriteBlankLine();
    WriteLine(enterMessageDescription);
    var message = ReadLineSafely();
    TimeSpan scheduleDelayTime = TimeSpan.Zero;
    bool isScheduled = false;

    if (!string.IsNullOrWhiteSpace(message))
    {
        if (message.Equals("exit", StringComparison.OrdinalIgnoreCase))
            break;

        WriteLine($"Would you like to Schedule this for the future? Y/N");
        var shouldScheduleResponse = ReadLineSafely();
        if (shouldScheduleResponse.Equals("Y", StringComparison.OrdinalIgnoreCase)
            || shouldScheduleResponse.Equals("Yes", StringComparison.OrdinalIgnoreCase)
        )
        {
            WriteLine($"Enter the amount of time to delay (e.g. 25s, 1.5m, 2h, 1d, etc.)...");
            WriteLine($"  - Otherwise leave blank to deliver immediately.");

            scheduleDelayTime = ReadLineSafely().TryParseTimeSpanWithUnitsAndMinutesDefault(out var parsedTimeSpan)
                ? parsedTimeSpan
                : TimeSpan.Zero;

            isScheduled = scheduleDelayTime > TimeSpan.Zero;
        }

        await outboxSender.SendMessageAsync(
            message,
            isScheduled ? scheduleDelayTime : null
        );

        WriteLine($"[{nameof(OutboxSender)}] Successfully Delivered Message into Outbox: {message}...");
        if (isScheduled)
            WriteLine($"  - Scheduled for Delivery in [{scheduleDelayTime.ToElapsedTimeDescriptiveFormat()}] and should arrive at ~[{DateTime.Now.Add(scheduleDelayTime)}])");
    }
}

WriteLine("Stopping Sql Transactional Outbox . . .");
//NOTE: This is not completely necessary as these will be stopped when Disposed of...
var processingExecutionCount = await outboxProcessor.StopProcessingAsync();
await messageReceiver.StopReceivingAsync();

WriteLine($"Sql Transactional Outbox Demo Stopped after Processing the Sql Transactional Outbox [{processingExecutionCount}] times!");
WriteBlankLine();
WriteLine($"Press any key to end...");
Console.ReadKey();
