# SqlTransactionalOutbox
A lightweight library & framework for implementing the Transactional Outbox pattern in .Net with default implementations for SQL Server & messaging via Azure Service Bus. Some of the key benefits offered:
1. Support for running in serverless environments (e.g. AzureFunctions) or in standard hosted .Net applications (via asynchronous background 'worker threads')
2. Support for enforcing true FIFO processing to preserve ordering.
3. Support for Scheduling Events to be sent at any point in the future.
4. A simplified abstractions for the Outbox, Outbox Processing, and Messaging systems utilized.
5. Solid Example Projects provided that are ready to run in Azure with **Azure SQL** & **Azure Service Bus**.

One of the main goals was to offer support for running in serverless environments such as **Azure Functions**, and the SqlTransactionalOutbox can be easily utilized either way: as hosted .Net Framework/.Net Core application (via fully asynchronous background 'worker threads'), or as a serverless Azure Functions deployment.

Another primary goal of the library/framework is to provide support for enforcing true FIFO processing to preserve ordering as well as providing safe coordination in horizontally scaled environments (e.g. serverless, or load balanced web servers) by providing robust distributed mutex locking (implemented elegantly via SQL Azure Database out-of-the-box functionality).

The library is completely interface based and extremely modular. In addition, all existing class methods are exposed as virtual methods to make it easy to customize existing implementations as needed, but ultimately we hope that the default implementations will work for the majority of use cases.

### Nuget Package (>=netstandard2.1)
To use this in your project, add the following packages:
- SQL Server Outbox: [SqlTransactionalOutbox.SqlServer.MicrosoftDataNS](https://www.nuget.org/packages/SqlTransactionalOutbox.SqlServer.MicrosoftDataNS/)
- Azure Service Bus Messaging: [SqlTransactionalOutbox.AzureServiceBus](https://www.nuget.org/packages/SqlTransactionalOutbox.AzureServiceBus/)

Or for your own customized implementations via Interfaces: [SqlTransactionalOutbox.Common](https://www.nuget.org/packages/SqlTransactionalOutbox.Common/)

### Give Star 🌟
**If you like this project and/or use it the please give it a Star 🌟 (c'mon it's free, and it'll help others find the project)!**

### [Buy me a Coffee ☕](https://www.buymeacoffee.com/cajuncoding)
*I'm happy to share with the community, but if you find this useful (e.g for professional use), and are so inclinded,
then I do love-me-some-coffee!*

<a href="https://www.buymeacoffee.com/cajuncoding" target="_blank">
<img src="https://cdn.buymeacoffee.com/buttons/default-orange.png" alt="Buy Me A Coffee" height="41" width="174">
</a>

## Examples - the best way to learn is to see and try!

The general flow of a Transactional Outbox has three primary steps:
1. **Send/Store items into the Outbox (transactionally)** for data integrity and to provide at-least-once-guaranteed delivery...
2. **Process the Outbox** and publish the items to the Service Bus (generally done asynchronously on a timer/interval)...
3. **Receive & handle the event message** to continue your business process/flow in an asynchronous handler...

Both of the example applications illustrate how to do this with two primary approaches:
1. **Azure Functions** (*serverless background processing*) -- low cost, highly scalable, and generally recommended approach for business/web applications.
2. **Console App** (*in-memory asynchronous background processing*) -- illustrates how you could manage this on your own server or application.

### Azure Functions Sample App
The Azure Functions Sample application provides a full blown implementation of the SQL Transactional Outbox as a fully background asynchronous process leveraging the serverless nature and very low cost of Azure Functions. This works very well and can scale for many business application scenarios.

The included sample project uses the `isolated process` model as that is the only model supported by Microsoft in the future.

The design of the Azure Functions project is pretty straightforward consisting of three functions as follows:
1. `TransactionalOutboxHttpProxySendPayloadFunction`: An HTTP Triggered function that takes in a generic Json payload and proxies it through as the Event payload. It dynamically parses various key aspects of the message, outbox processing item, etc. from the json illustrating how it can be used in extremely flexible manner to handle any kind of payloads.
2. `TransactionalOutboxAgentFunction`: A CRON Timer Triggered function that runs and attempts to Process the Outbox on an interval. This would usually run on a interval based on your requirements for delivery latency/throughput, server resources, costs, etc. Usually every 30 seconds or 1 minute is good; but any interval is fine. It is usually desirable to ensure that only one processing agent attempts to process the outbox at a time which is easily implemented with a Distributed Mutex that is facilitated by SQL Server (you already have it readily available) and illustrated in the Example App using the [SqlAppLockHelper Library](https://github.com/cajuncoding/SqlAppLockHelper).
3. `TransactionalOutboxFifoReceiverFunction`: An Azure Service Bus Triggered function that handles all Event messages published and illustrates how they can be loaded/re-hydrated from the Event Message and handled. The example app providesa  simple log stream, but a real world implementation would likely use the message to drive business processes, delegating to background Jobs via Strategy Pattern, etc.
   1. **NOTE:** *This is named/configured as FIFO because my test instance of Azure Service Bus uses Sessions which explicitly enables sorted processing (aka FIFO) and this ensures that sorted processing is properly tested in the integration tests. This would need to be adjusted if your Service Bus Topic/Subscription does not have Sessions enabled in Azure Service Bus.*

### Console Sample App
The Console sample app is intended to illustrate how you can use the (provided) `AsyncThreadOutboxProcessingAgent` in a custom implementation that runs asynchronously in-memory but works just as well in a console app context as a full blown ASP.Net web application.

Just as above the same concepts are modeled in the classes that encapsulate the logic for the three main steps:
1. `OutboxSender`: Encapsulates the step of sending/storing an event in the Transactional Outbox. It uses a standard `SqlConnection` and the `AddTransactionalOutboxPendingItemAsync(...)` extension provided by the library.
2. `OutboxProcessor`: Encapsulates the step of processing the outbox in fully asynchronous manner to provide background processing that has no impact on the UX -- in this case the console window. It leverages the `DefaultAzureServiceBusOutboxPublisher` which is a dependency for the `AsyncThreadOutboxProcessingAgent` to provide all asynchronous processing & publishing to the Azure Service Bus.
3. `OutboxFifoReceiver`: Encapsulates the additional asynchronous processing to receive and handle all messages delivered by the Azure Service Bus. It leverages the `DefaultFifoAzureServiceBusReceiver` to simplify the various complexities of properly wiring-up the underlying `ServiceBusSessionProcessor`/`ServiceBusProcessor`. This significantly streamlines the code & effort needed to manually do this.

All of these classes are then orchestrated in a small console app that allows you to enter `string` messages that are sent as event payloads. It asks you if you'd like to send it immediately or schedule it for delayed delivery. If you opt to delay it `Y/N` , you may then enter an amount of time (e.g. `30s`, `1.5m`, `4h`, `1d`, etc.). The message will then be scheduled for delivery by calculating the resulting scheduled publish time.

Various updates on the process are streamed to the console, and ultimately once events are published then they are asynchronously received and the results are streamed to the console in-line also -- making for a nice little demonstration of the entire process.

The process for a series of messages that were delivered immediately, aftef 1 minute, after 30 mins, after 1 hour, and then after 8 hours resulted in the following:
<div align="center"><img src="./Scheduled%20Delivery%20-%20CommandLine%20Demo%20-%202026-03-03.png" width="600" /></div>

## Initialization & DB Schema Setup
The Sql Transactional Outbox provides uses several default values that can be customized at initialization
so that all the convenience methods (e.g. Sql Connection/Transaction custom extensions) work as expected with 
the values you need.

NOTES:
- *This should only be done in your applications' startup/initialization (e.g. application root, Program.cs, Startup.cs, etc.).*
- *You can use your own schema & table as long as you have all of these fields and their data types match. The actual schema, table & column/field names are fully customizable with the configuration api shown below*👇...

```csharp
    //This is the global SqlTransactionalOutbox initializer that allows configuring custom settings to be used...
    //NOTE: Not all values need to be specified, any values that are not specified (e.g. or are set to null)
    //      will retain the default values.
    SqlTransactionalOutboxInitializer.Configure(config =>
    {
        config
            .WithOutboxTableConfig(new OutboxTableConfig(
                transactionalOutboxSchemaName: "...",
                transactionalOutboxTableName: "...",
                pkeyFieldName: "...",
                payloadFieldName: "...",
                uniqueIdentifierFieldName: "...",
                fifoGroupingIdentifier: "...",
                statusFieldName: "...",
                publishTargetFieldName: "...",
                publishAttemptsFieldName: "...",
                createdDateTimeUtcFieldName: "..."
            ))
            .WithDistributedMutexLockSettings(
                lockAcquisitionTimeoutSeconds: 1,
                lockNamePrefix: "..."
            )
            .ConfigureOutboxProcessingOptions(options =>
            {
                //ItemProcessingBatchSize = 200, //Only process the top X items to keep this function responsive!
                options.FifoEnforcedPublishingEnabled = true; //Must set based on whether the Azure Service Bus Topic is Session Enabled.
                options.MaxPublishingAttempts = appConfig.OutboxMaxPublishingRetryAttempts;
                options.TimeSpanToLive = appConfig.OutboxMaxTimeToLiveTimeSpan;
                options.TimeSpanToLive = TimeSpan.FromHours(1);
                //Optimize our Scheduled Delivery by pre-fetching items . . . 
                //NOTE: By tuning this value in combination with the Outbox Processing Interval (e.g. TransactionalOutboxAgentCronSchedule) you can ensure item delivery is
                //  as close as possible to the actual Scheduled Publish Time while still allowing for some tolerance to ensure items are not missed due to processing delays, etc.!
                //FOR EXAMPLE: If your Outbox Processing Interval is every 1 minute and you want to ensure items are published as close as possible to their Scheduled Publish Time
                //  then you may want to set the prefetch time to 2 minutes (or 5 minutes) -- slightly to marginally higher than the processing interval -- to ensure items
                //  are published to Azure Service Bus before their scheduled time and allow some tolerance for processing delays, etc.!
                //  This may also be impacted by other configuration options such as overall outbox load/throughput, processing batch sizes, etc.
                options.ScheduledPublishPrefetchTime = TimeSpan.FromMinutes(2);
            })            .;
    });
```

## Database Schema:
The schema used for the SQL Server implementation is as follows.  This is also stored in the project here:
(SqlTransactionalOutbox.SqlServer.Common => _SqlScript => TransactionalOutboxSqlScript.sql)
[https://github.com/cajuncoding/SqlTransactionalOutbox/blob/main/SqlTransactionalOutbox.SqlServer.Common/_SqlScript/TransactionalOutboxSqlScript.sql]
```sql
IF NOT EXISTS (SELECT name FROM sys.schemas WHERE name = N'notifications')
BEGIN
    EXEC('CREATE SCHEMA notifications;');
END
GO

DROP TABLE IF EXISTS [notifications].[TransactionalOutboxQueue];
CREATE TABLE [notifications].[TransactionalOutboxQueue] (
	[Id] INT IDENTITY NOT NULL,
	[UniqueIdentifier] UNIQUEIDENTIFIER NOT NULL,
	[FifoGroupingIdentifier] VARCHAR(200) NULL,
	[Status] VARCHAR(50) NOT NULL,
	[CreatedDateTimeUtc] DATETIME2 NOT NULL DEFAULT SysUtcDateTime(),
	[ScheduledPublishDateTimeUtc] DATETIME2 NULL DEFAULT NULL,
	[PublishAttempts] INT NOT NULL DEFAULT 0,
	[PublishTarget] VARCHAR(200) NOT NULL, -- Topic and/or Queue name
	[Payload] NVARCHAR(MAX), -- Generic Payload supporting Implementation specific processing (e.g. Json)
	CONSTRAINT [PKEY_TransactionalOutboxQueue_Id] PRIMARY KEY ([Id])
);
GO

CREATE NONCLUSTERED INDEX [IDX_TransactionalOutboxQueue_UniqueIdentifier] ON [notifications].[TransactionalOutboxQueue] ([UniqueIdentifier]);
GO

--Remove the old v1.0.x index and create a new one with the ScheduledPublishDateTimeUtc column to support the new scheduling feature of v1.1.x.
-- This will allow for more efficient querying of messages that are scheduled to be published at a specific time.
DROP INDEX IF EXISTS [IDX_TransactionalOutboxQueue_Status] ON [notifications].[TransactionalOutboxQueue];

CREATE NONCLUSTERED INDEX [IDX_TransactionalOutboxQueue_Status_ScheduledPublishDateTimeUtc]
    ON [notifications].[TransactionalOutboxQueue] ([Status], [ScheduledPublishDateTimeUtc]);
GO
```

### Migration Script from v1.0.x to v1.1.x:
```sql
-- Simply add the new column if missing...
IF COL_LENGTH('notifications.TransactionalOutboxQueue', 'ScheduledPublishDateTimeUtc') IS NULL
    ALTER TABLE [notifications].[TransactionalOutboxQueue] ADD [ScheduledPublishDateTimeUtc] DATETIME2 NULL;
GO

--Migrate the Indexes for best performance...
--Remove the old v1.0.x index and create a new one with the ScheduledPublishDateTimeUtc column to support the new scheduling feature of v1.1.x.
-- This will allow for more efficient querying of messages that are scheduled to be published at a specific time.
DROP INDEX IF EXISTS [IDX_TransactionalOutboxQueue_Status] ON [notifications].[TransactionalOutboxQueue];

CREATE NONCLUSTERED INDEX [IDX_TransactionalOutboxQueue_Status_ScheduledPublishDateTimeUtc]
    ON [notifications].[TransactionalOutboxQueue] ([Status], [ScheduledPublishDateTimeUtc]);
GO

```

## Release Notes v1.1.0:
- New support for scheduled publishing of Events from the Outbox via `ScheduledPublishDateTimeUtc` property.
  - Azure Service Bus actually has delayed delivery feature so that is now fully supported also to get best of all worlds:
    - Messages are scheduled in the outbox and can live there until they are published -- which is ideally shortly before the actual scheduled delivery time. Keeping the payloads in the outbox provides  easier observability and resiliency as it's persisted in the SQL Server Database.    
    - Then by tuning the prefetch configuration value `OutboxProcessingOptions.ScheduledPublishPrefetchTime` in combination with  how often the Outbox Processing Agent runs (timer interval) you can achieve delivery at (nearly) exactly the scheduled time.
- Significant updates to the Sample Apps have been added and fully illustrate the new Scheduled delivery feature.
- Azure Functions Sample App has been fully migrated to the `isolated process` model; as the legacy `in-process` model is deprecated.
- Implemented new SQL reader performance improvements and safety for handling NULL values.
- Fixed AsyncThreadOutboxProcessingAgent to correctly implement distributed locking by default as a best practice; with parameters to support disabling and customizing the lock name.
- Add full support for CancellationToken throughout the API as it was missing!
- Various code cleanup, Json parsing fixes (to now support DateTime), and stability improvements.
  - Most are non-breaking changes and optional additions.
- Added convenience support for the `PayloadBuilder` helper class to handle `ScheduledPublishDateTimeUtc` and to parse `ScheduledPublishDelay` using simplified syntax (e.g. 30s, 5m, 4.5h, etc.) or standard TimeSpan syntax.
  - Default integer parsing is in minutes (not hours like standard TimeSpan parsing).
- Synced version across all packages.

## Release Notes v1.0.4 / v1.0.5 (mixed versions depending on the package):
 - Reverted Microsoft.Data.SqlClient package to version 5.2.3 to maintain compatibility with .Net 6.0 for existing applicaitons not yet updated.

## Release Notes v1.0.3:
- Update Microsoft.Data.SqlClient package to new version to resolve vulnerability risks in older version.
- Update System.Data.SqlClient package to new version to resolve vulnerability risks in older version.

## Release Notes v1.0.2:
- Fix bug in DefaultSqlServerOutboxRepository to use new customizable global configuration as Default.

## Release Notes v1.0.1:
- Improved support for customizing OutboxTable Configuration and Distributed Mutex Lock settings via SqlTransactionalOutboxInitializer.Configure() initialization.

## Release Notes v1.0.0:
- (Breaking Changes) Fully migrated (refactored) to now use `Azure.Messaging.ServiceBus` SDK/Library for future support; other Azure Service Bus libraries are all now fully deprecated by Microsoft.
- The main breaking change is now the use of ServiceBusReceivedMessage vs deprecated Message object.
- All Interfaces and the genearl abstraction are still valid so code updates are straightforward.
- This now enables Azure Functions v4 (with .Net 6) to work as expected with AzureServiceBus bindings (requires ServiceBusReceivedMessage).
- Also fixed several bugs/issues, and optimized Options and Naming which may also have some small Breaking Changes.
- Improved Error Handling when Processing of Outbox has unexpected Exceptions.
- Also added a new Default implementation for `AsyncThreadOutboxProcessingAgent` (to run the Processing in an async Thread; ideal for AspNet Applications).
- Improved Json serialization to eliminate unnecessary storing of Null properties and consistently use camelCase Json.
- Added full Console Sample Application (in Github Source) that provides Demo of the full lifecycle of the Sql Transactional Outbox.

### Prior Release Notes
- BETA Release v0.0.1: The library is current being shared/released in a _Beta_ form. It is being actively used for a variety of projects, and as the confidence in the functionality and stability increases through testing we will update and provide a full release. Release notes and detais will be posted here as needed.


## Documentation TODOs:
Provide documentation for:
 - Transactional Outbox Pattern summary/overview
 - Simplified usage of default implementations using easy to consume CustomExtensions.
 - Advanced usage of default implementations with Options
 - Summary of details for customizing implementations as needed (e.g. Different Publishing implementation)
