CREATE SCHEMA notifications;
GO

--DROP TABLE [notifications].[TransactionalOutboxQueue];

CREATE TABLE [notifications].[TransactionalOutboxQueue] (
	[UniqueIdentifier] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
	[Status] VARCHAR(10) NOT NULL,
	[PublshingTarget] VARCHAR(200) NOT NULL, -- Topic and/or Queue name
	[PublishingPayload] NVARCHAR(MAX), -- Payload genercially processed by an implementation (e.g. Json)
	[PublishingAttempts] INT NOT NULL DEFAULT 0,
	[CreatedDateTimeUtc] DATETIME2 NOT NULL DEFAULT SysUtcDateTime(),
);
GO