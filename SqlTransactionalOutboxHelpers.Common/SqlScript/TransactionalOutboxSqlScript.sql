CREATE SCHEMA notifications;
GO

CREATE TABLE [notifications].[TransactionalOutboxQueue] (
	[UUID] VARCHAR(38) PRIMARY KEY,
	[Status] VARCHAR(10),
	[PublshingTarget] VARCHAR(200), -- Topic and/or Queue name
	[PublishingPayload] NVARCHAR(MAX), -- Payload genercially processed by an implementation (e.g. Json)
	[CreatedDateTimeUtc] DATETIME2,
	[ExpirationDateTimeUtc] DATETIME2
);
GO