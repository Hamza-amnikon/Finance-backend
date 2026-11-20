BEGIN TRANSACTION;
IF NOT EXISTS (SELECT 1 FROM security.Permissions WHERE PermissionCode = N'banking.internal-payment-requests')
BEGIN
    INSERT INTO security.Permissions
        (PermissionCode, Module, PageName, Route, Status, CreatedDate)
    VALUES
        (N'banking.internal-payment-requests', N'Banking', N'Internal Payment Requests',
         N'/banking/internal-payment-requests', N'Active', SYSUTCDATETIME());
END;

CREATE TABLE [banking].[InternalPaymentRequests] (
    [Id] int NOT NULL IDENTITY,
    [RequestId] uniqueidentifier NOT NULL,
    [RequestingCompanyId] int NOT NULL,
    [PayingCompanyId] int NOT NULL,
    [ReceivingBankAccountId] int NOT NULL,
    [RequestDate] date NOT NULL,
    [RequiredDate] date NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [CurrencyDecimalPlaces] int NOT NULL,
    [Amount] decimal(18,4) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [Notes] nvarchar(2000) NOT NULL,
    [DecisionNote] nvarchar(500) NOT NULL,
    [CreatedBy] nvarchar(200) NOT NULL,
    [DecidedBy] nvarchar(200) NULL,
    [CreatedDate] datetime2 NOT NULL,
    [ModifiedDate] datetime2 NULL,
    [SubmittedDate] datetime2 NULL,
    [DecidedDate] datetime2 NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_InternalPaymentRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_InternalPaymentRequest_Amount] CHECK ([Amount] > 0),
    CONSTRAINT [CK_InternalPaymentRequest_Companies] CHECK ([RequestingCompanyId] <> [PayingCompanyId]),
    CONSTRAINT [CK_InternalPaymentRequest_Dates] CHECK ([RequiredDate] >= [RequestDate]),
    CONSTRAINT [CK_InternalPaymentRequest_Status] CHECK ([Status] IN ('Draft', 'Submitted', 'Approved', 'Rejected', 'Cancelled')),
    CONSTRAINT [FK_InternalPaymentRequests_BankAccounts_ReceivingBankAccountId_RequestingCompanyId] FOREIGN KEY ([ReceivingBankAccountId], [RequestingCompanyId]) REFERENCES [expenses].[BankAccounts] ([Id], [CompanyId]) ON DELETE NO ACTION
);

CREATE TABLE [banking].[InternalPaymentRequestLines] (
    [Id] int NOT NULL IDENTITY,
    [InternalPaymentRequestId] int NOT NULL,
    [Purpose] nvarchar(200) NOT NULL,
    [Amount] decimal(18,4) NOT NULL,
    CONSTRAINT [PK_InternalPaymentRequestLines] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_InternalPaymentRequestLine_Amount] CHECK ([Amount] > 0),
    CONSTRAINT [FK_InternalPaymentRequestLines_InternalPaymentRequests_InternalPaymentRequestId] FOREIGN KEY ([InternalPaymentRequestId]) REFERENCES [banking].[InternalPaymentRequests] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_InternalPaymentRequestLines_InternalPaymentRequestId] ON [banking].[InternalPaymentRequestLines] ([InternalPaymentRequestId]);

CREATE INDEX [IX_InternalPaymentRequests_PayingCompanyId_Status] ON [banking].[InternalPaymentRequests] ([PayingCompanyId], [Status]);

CREATE INDEX [IX_InternalPaymentRequests_ReceivingBankAccountId_RequestingCompanyId] ON [banking].[InternalPaymentRequests] ([ReceivingBankAccountId], [RequestingCompanyId]);

CREATE UNIQUE INDEX [IX_InternalPaymentRequests_RequestId] ON [banking].[InternalPaymentRequests] ([RequestId]);

CREATE INDEX [IX_InternalPaymentRequests_RequestingCompanyId_RequestDate] ON [banking].[InternalPaymentRequests] ([RequestingCompanyId], [RequestDate]);

INSERT INTO [banking].[__BankingMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261119135924_AddInternalPaymentRequests', N'10.0.12');

COMMIT;
GO

