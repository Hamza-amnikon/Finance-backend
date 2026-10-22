BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [sales].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022134058_LinkInvoicePaymentsToBanking'
)
BEGIN
    ALTER TABLE [sales].[SalesInvoicePayments] ADD [BankAccountId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [sales].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022134058_LinkInvoicePaymentsToBanking'
)
BEGIN
    ALTER TABLE [sales].[SalesInvoicePayments] ADD [BankAccountName] nvarchar(160) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [sales].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022134058_LinkInvoicePaymentsToBanking'
)
BEGIN
    ALTER TABLE [sales].[SalesInvoicePayments] ADD [BankAmount] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [sales].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022134058_LinkInvoicePaymentsToBanking'
)
BEGIN
    ALTER TABLE [sales].[SalesInvoicePayments] ADD [BankCurrency] nvarchar(3) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [sales].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022134058_LinkInvoicePaymentsToBanking'
)
BEGIN
    ALTER TABLE [sales].[SalesInvoicePayments] ADD [BankTransactionId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [sales].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022134058_LinkInvoicePaymentsToBanking'
)
BEGIN
    ALTER TABLE [sales].[SalesInvoicePayments] ADD [SettlementType] nvarchar(10) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [sales].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022134058_LinkInvoicePaymentsToBanking'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SalesInvoicePayments_BankTransactionId] ON [sales].[SalesInvoicePayments] ([BankTransactionId]) WHERE [BankTransactionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [sales].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022134058_LinkInvoicePaymentsToBanking'
)
BEGIN
    INSERT INTO [sales].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022134058_LinkInvoicePaymentsToBanking', N'10.0.12');
END;

COMMIT;
GO

