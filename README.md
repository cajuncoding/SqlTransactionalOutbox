# SqlTransactionalOutbox
A lightweight library & framework for implementing the Transactional Outbox pattern in .Net with default implementaions for SQL Server & messaging via Azure Service Bus. Some of the key benefits offered are support for running in serverless environments (e.g. AzureFunctions) or in standard hosted .Net applications (via 'worker threads'), and support for enforcing true FIFO processing to preserve ordering, and a simplified abstractions for the Outbox, Outbox Processing, and Messaging systems utilized.

One of the main goals was to offer support for running in serverless environments such as Azure Functions, and the SqlTransactionalOutbox can be easily utilized either way: as hosted .Net Framework/.Net Core application (via 'worker threads'), or as a serverless Azure Functions deployment. Another primary goal of the library is to provide support for enforcing true FIFO processing to preserve ordering as well as providing safe coordination in horizontally scaled environments (e.g. serverless, or load balanced web servers).

The library is completely interface based and extremely modular. In addition, all existing class methods are exposed as virtual methods to make it easy to customize existing implementations as needed, but ultimately we hope that the default implementations will work for the majority of use cases.

### Nuget Package (>=netstandard2.1)
To use this in your project, add the [GraphQL.PreprocessingExtensions](https://www.nuget.org/packages/GraphQL.PreProcessingExtensions/) 
NuGet package to your project, wire up your Starup middleware, and inject / instantiate params in your resolvers as outlined below...

### [Buy me a Coffee ☕](https://www.buymeacoffee.com/cajuncoding)
*I'm happy to share with the community, but if you find this useful (e.g for professional use), and are so inclinded,
then I do love-me-some-coffee!*

<a href="https://www.buymeacoffee.com/cajuncoding" target="_blank">
<img src="https://cdn.buymeacoffee.com/buttons/default-orange.png" alt="Buy Me A Coffee" height="41" width="174">
</a>

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

## Initialization
The Sql Transactional Outbox provides uses several default values that can be customized at initialization
so that all the convenience methods (e.g. Sql Connection/Transaction custom extensions) work as expected with 
the values you need.
*NOTE: This should only be done in your applications' startup/initialization (e.g. application root, Program.cs, Startup.cs, etc.).*

```csharp
    //This is the global SqlTransactionalOutbox initializer that allows configuring custom settings to be used...
    //NOTE: Not all values need to be specified, any values that are not specified (e.g. or are set to null)
    //      will retain the default values.
    SqlTransactionalOutboxInitializer.Configure(config =>
    {
        config.WithOutboxTableConfig(new OutboxTableConfig(
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
            );
    });
```

## Database Schema:
The schema used for the SQL Server implementation is as follows.  This is also stored in the project here:
(SqlTransactionalOutbox.SqlServer.Common => _SqlScript => TransactionalOutboxSqlScript.sql)
[https://github.com/cajuncoding/SqlTransactionalOutbox/blob/main/SqlTransactionalOutbox.SqlServer.Common/_SqlScript/TransactionalOutboxSqlScript.sql]
```sql
    CREATE SCHEMA notifications;
    GO

    --DROP TABLE [notifications].[TransactionalOutboxQueue];
    CREATE TABLE [notifications].[TransactionalOutboxQueue] (
	    [Id] INT IDENTITY NOT NULL PRIMARY KEY,
	    [UniqueIdentifier] UNIQUEIDENTIFIER NOT NULL,
	    [FifoGroupingIdentifier] VARCHAR(200) NULL,
	    [Status] VARCHAR(50) NOT NULL,
	    [CreatedDateTimeUtc] DATETIME2 NOT NULL DEFAULT SysUtcDateTime(),
	    [PublishAttempts] INT NOT NULL DEFAULT 0,
	    [PublishTarget] VARCHAR(200) NOT NULL, -- Topic and/or Queue name
	    [Payload] NVARCHAR(MAX), -- Generic Payload supporting Implementation specific processing (e.g. Json)
    );
    GO

    CREATE NONCLUSTERED INDEX [IDX_TransactionalOutboxQueue_UniqueIdentifier] ON [notifications].[TransactionalOutboxQueue] ([UniqueIdentifier]);
    GO

    CREATE NONCLUSTERED INDEX [IDX_TransactionalOutboxQueue_Status] ON [notifications].[TransactionalOutboxQueue] ([Status]);
    GO
```

## Documentation TODOs:
Provide documentation for:
 - Transactional Outbox Pattern summary/overview
 - Simplified usage of default implementations using easy to consume CustomExtensions.
 - Advanced usage of default implementations with Options
 - Summary of details for customizing impleentations as needed (e.g. Different Publishing implementation)
 - Provide link directly to SQL Script for Default table schema creation
