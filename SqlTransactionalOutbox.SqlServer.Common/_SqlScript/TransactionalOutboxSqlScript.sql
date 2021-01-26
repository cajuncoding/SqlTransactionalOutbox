CREATE SCHEMA notifications;
GO

--DROP TABLE [notifications].[TransactionalOutboxQueue];

CREATE TABLE [notifications].[TransactionalOutboxQueue] (
	[Id] INT IDENTITY NOT NULL PRIMARY KEY,
	[UniqueIdentifier] UNIQUEIDENTIFIER NOT NULL,
	[Status] VARCHAR(50) NOT NULL,
	[CreatedDateTimeUtc] DATETIME2 NOT NULL DEFAULT SysUtcDateTime(),
	[PublishAttempts] INT NOT NULL DEFAULT 0,
	[PublishTarget] VARCHAR(200) NOT NULL, -- Topic and/or Queue name
	[PublishPayload] NVARCHAR(MAX), -- Payload genercially processed by an implementation (e.g. Json)
);
GO

CREATE NONCLUSTERED INDEX [IDX_TransactionalOutboxQueue_UniqueIdentifier] ON [notifications].[TransactionalOutboxQueue] ([UniqueIdentifier]);
GO

CREATE NONCLUSTERED INDEX [IDX_TransactionalOutboxQueue_Status] ON [notifications].[TransactionalOutboxQueue] ([Status]);
GO