BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [purchases].[__PaymentMadeMigrationsHistory]
    WHERE [MigrationId] = N'20261022134053_LinkPaymentsMadeToBanking'
)
BEGIN
    ALTER TABLE [purchases].[PaymentsMade] ADD [BankAccountId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [purchases].[__PaymentMadeMigrationsHistory]
    WHERE [MigrationId] = N'20261022134053_LinkPaymentsMadeToBanking'
)
BEGIN
    ALTER TABLE [purchases].[PaymentsMade] ADD [BankAccountName] nvarchar(160) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [purchases].[__PaymentMadeMigrationsHistory]
    WHERE [MigrationId] = N'20261022134053_LinkPaymentsMadeToBanking'
)
BEGIN
    ALTER TABLE [purchases].[PaymentsMade] ADD [BankAmount] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [purchases].[__PaymentMadeMigrationsHistory]
    WHERE [MigrationId] = N'20261022134053_LinkPaymentsMadeToBanking'
)
BEGIN
    ALTER TABLE [purchases].[PaymentsMade] ADD [BankCurrency] nvarchar(3) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [purchases].[__PaymentMadeMigrationsHistory]
    WHERE [MigrationId] = N'20261022134053_LinkPaymentsMadeToBanking'
)
BEGIN
    ALTER TABLE [purchases].[PaymentsMade] ADD [BankReversalTransactionId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [purchases].[__PaymentMadeMigrationsHistory]
    WHERE [MigrationId] = N'20261022134053_LinkPaymentsMadeToBanking'
)
BEGIN
    ALTER TABLE [purchases].[PaymentsMade] ADD [BankTransactionId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [purchases].[__PaymentMadeMigrationsHistory]
    WHERE [MigrationId] = N'20261022134053_LinkPaymentsMadeToBanking'
)
BEGIN
    ALTER TABLE [purchases].[PaymentsMade] ADD [SettlementType] nvarchar(10) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [purchases].[__PaymentMadeMigrationsHistory]
    WHERE [MigrationId] = N'20261022134053_LinkPaymentsMadeToBanking'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PaymentsMade_BankTransactionId] ON [purchases].[PaymentsMade] ([BankTransactionId]) WHERE [BankTransactionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [purchases].[__PaymentMadeMigrationsHistory]
    WHERE [MigrationId] = N'20261022134053_LinkPaymentsMadeToBanking'
)
BEGIN
    INSERT INTO [purchases].[__PaymentMadeMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022134053_LinkPaymentsMadeToBanking', N'10.0.12');
END;

COMMIT;
GO

