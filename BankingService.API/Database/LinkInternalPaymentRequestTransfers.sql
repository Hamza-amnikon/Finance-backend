BEGIN TRANSACTION;
CREATE TABLE [banking].[InternalPaymentRequestTransfers] (
    [Id] int NOT NULL IDENTITY,
    [InternalPaymentRequestId] int NOT NULL,
    [BankTransferId] int NOT NULL,
    [LinkedDate] datetime2 NOT NULL,
    [LinkedBy] nvarchar(200) NOT NULL,
    CONSTRAINT [PK_InternalPaymentRequestTransfers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InternalPaymentRequestTransfers_BankTransfers_BankTransferId] FOREIGN KEY ([BankTransferId]) REFERENCES [banking].[BankTransfers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InternalPaymentRequestTransfers_InternalPaymentRequests_InternalPaymentRequestId] FOREIGN KEY ([InternalPaymentRequestId]) REFERENCES [banking].[InternalPaymentRequests] ([Id]) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX [IX_InternalPaymentRequestTransfers_BankTransferId] ON [banking].[InternalPaymentRequestTransfers] ([BankTransferId]);

CREATE INDEX [IX_InternalPaymentRequestTransfers_InternalPaymentRequestId] ON [banking].[InternalPaymentRequestTransfers] ([InternalPaymentRequestId]);

INSERT INTO [banking].[__BankingMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261119142137_LinkInternalPaymentRequestTransfers', N'10.0.12');

COMMIT;
GO

