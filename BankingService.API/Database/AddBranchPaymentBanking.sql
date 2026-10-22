BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [banking].[__BankingMigrationsHistory]
    WHERE [MigrationId] = N'20261022153935_AddBranchPaymentBanking'
)
BEGIN
    CREATE TABLE [banking].[PaymentBankingLinks] (
        [Id] int NOT NULL IDENTITY,
        [Source] nvarchar(20) NOT NULL,
        [SourcePaymentId] int NOT NULL,
        [SourceRequestId] uniqueidentifier NOT NULL,
        [PaymentNumber] nvarchar(50) NOT NULL,
        [Branch] nvarchar(20) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [Amount] decimal(18,4) NOT NULL,
        [BankTransactionId] int NULL,
        [ReversalTransactionId] int NULL,
        [State] nvarchar(20) NOT NULL,
        [Message] nvarchar(1000) NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [NextAttemptAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PaymentBankingLinks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentBankingLinks_BankTransactions_BankTransactionId] FOREIGN KEY ([BankTransactionId]) REFERENCES [banking].[BankTransactions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PaymentBankingLinks_BankTransactions_ReversalTransactionId] FOREIGN KEY ([ReversalTransactionId]) REFERENCES [banking].[BankTransactions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [banking].[__BankingMigrationsHistory]
    WHERE [MigrationId] = N'20261022153935_AddBranchPaymentBanking'
)
BEGIN
    CREATE TABLE [banking].[PaymentBankingSettings] (
        [Id] int NOT NULL,
        [SourceAfterUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PaymentBankingSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [banking].[__BankingMigrationsHistory]
    WHERE [MigrationId] = N'20261022153935_AddBranchPaymentBanking'
)
BEGIN
    CREATE INDEX [IX_PaymentBankingLinks_BankTransactionId] ON [banking].[PaymentBankingLinks] ([BankTransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [banking].[__BankingMigrationsHistory]
    WHERE [MigrationId] = N'20261022153935_AddBranchPaymentBanking'
)
BEGIN
    CREATE INDEX [IX_PaymentBankingLinks_ReversalTransactionId] ON [banking].[PaymentBankingLinks] ([ReversalTransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [banking].[__BankingMigrationsHistory]
    WHERE [MigrationId] = N'20261022153935_AddBranchPaymentBanking'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentBankingLinks_Source_SourcePaymentId] ON [banking].[PaymentBankingLinks] ([Source], [SourcePaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [banking].[__BankingMigrationsHistory]
    WHERE [MigrationId] = N'20261022153935_AddBranchPaymentBanking'
)
BEGIN
    CREATE INDEX [IX_PaymentBankingLinks_State_NextAttemptAtUtc] ON [banking].[PaymentBankingLinks] ([State], [NextAttemptAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [banking].[__BankingMigrationsHistory]
    WHERE [MigrationId] = N'20261022153935_AddBranchPaymentBanking'
)
BEGIN
    INSERT INTO [banking].[PaymentBankingSettings] ([Id], [SourceAfterUtc]) VALUES (1, SYSUTCDATETIME());
END;

IF NOT EXISTS (
    SELECT * FROM [banking].[__BankingMigrationsHistory]
    WHERE [MigrationId] = N'20261022153935_AddBranchPaymentBanking'
)
BEGIN
    INSERT INTO [banking].[__BankingMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022153935_AddBranchPaymentBanking', N'10.0.12');
END;

COMMIT;
GO

