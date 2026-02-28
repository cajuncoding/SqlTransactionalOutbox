IF NOT EXISTS (SELECT name FROM sys.schemas WHERE name = N'notifications')
BEGIN
    EXEC('CREATE SCHEMA notifications;');
END
GO

DROP TABLE IF EXISTS [notifications].[TransactionalOutboxQueue];
CREATE TABLE [notifications].[TransactionalOutboxQueue] (
	[Id] INT IDENTITY NOT NULL PRIMARY KEY,
	[UniqueIdentifier] UNIQUEIDENTIFIER NOT NULL,
	[FifoGroupingIdentifier] VARCHAR(200) NULL,
	[Status] VARCHAR(50) NOT NULL,
	[CreatedDateTimeUtc] DATETIME2 NOT NULL DEFAULT SysUtcDateTime(),
	[ScheduledPublishDateTimeUtc] DATETIME2 NULL DEFAULT NULL,
	[PublishAttempts] INT NOT NULL DEFAULT 0,
	[PublishTarget] VARCHAR(200) NOT NULL, -- Topic and/or Queue name
	[Payload] NVARCHAR(MAX), -- Generic Payload supporting Implementation specific processing (e.g. Json)
);
GO

CREATE NONCLUSTERED INDEX [IDX_TransactionalOutboxQueue_UniqueIdentifier] ON [notifications].[TransactionalOutboxQueue] ([UniqueIdentifier]);
GO

--Remove the old v1.0.x index and create a new one with the ScheduledPublishDateTimeUtc column to support the new scheduling feature of v1.1.x.
-- This will allow for more efficient querying of messages that are scheduled to be published at a specific time.
DROP INDEX IF EXISTS [IDX_TransactionalOutboxQueue_Status] ON [notifications].[TransactionalOutboxQueue];
GO

CREATE NONCLUSTERED INDEX [IDX_TransactionalOutboxQueue_Status_ScheduledPublishDateTimeUtc] ON [notifications].[TransactionalOutboxQueue] ([Status], [ScheduledPublishDateTimeUtc]);
GO