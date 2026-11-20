BEGIN TRANSACTION;
ALTER TABLE [banking].[InternalPaymentRequests] ADD [CancellationNote] nvarchar(500) NULL;

ALTER TABLE [banking].[InternalPaymentRequests] ADD [CancelledBy] nvarchar(200) NULL;

ALTER TABLE [banking].[InternalPaymentRequests] ADD [CancelledDate] datetime2 NULL;

ALTER TABLE [banking].[InternalPaymentRequests] ADD [SubmissionNote] nvarchar(500) NULL;

ALTER TABLE [banking].[InternalPaymentRequests] ADD [SubmittedBy] nvarchar(200) NULL;

INSERT INTO [banking].[__BankingMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261120102806_AddRequestActionAudit', N'10.0.12');

COMMIT;
GO

