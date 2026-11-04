SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

IF DB_NAME() <> N'SparkFinance'
    THROW 50001, 'This script only runs against SparkFinance.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DELETE FROM [sales].[CustomerCreditApplications];
    DELETE FROM [sales].[SalesCreditNoteItems];
    DELETE FROM [sales].[SalesCreditNotes];
    DELETE FROM [sales].[SalesInvoicePayments];
    DELETE FROM [sales].[SalesInvoiceItems];
    DELETE FROM [sales].[SalesInvoices];
    DELETE FROM [sales].[CreditNoteNumberCounters];
    DELETE FROM [sales].[InvoiceNumberCounters];

    DELETE FROM [estimates].[SalesEstimateItems];
    DELETE FROM [estimates].[SalesEstimates];
    DELETE FROM [estimates].[EstimateNumberCounters];

    DELETE FROM [banking].[PaymentBankingLinks];
    DELETE FROM [banking].[BankStatementLines];
    DELETE FROM [banking].[BankReconciliations];
    DELETE FROM [banking].[BankTransfers];
    UPDATE [banking].[BankTransactions]
    SET [ReversalOfTransactionId] = NULL
    WHERE [ReversalOfTransactionId] IS NOT NULL;
    DELETE FROM [banking].[BankTransactions];

    DELETE FROM [accounting].[JournalEntryLines];
    UPDATE [accounting].[JournalEntries]
    SET [ReversalOfJournalEntryId] = NULL
    WHERE [ReversalOfJournalEntryId] IS NOT NULL;
    DELETE FROM [accounting].[JournalEntries];

    DELETE FROM [expenses].[Receipts];
    DELETE FROM [expenses].[Expenses];
    DELETE FROM [expenses].[BankAccounts];

    DELETE FROM [purchases].[VendorCreditApplications];
    DELETE FROM [purchases].[VendorCreditItems];
    DELETE FROM [purchases].[VendorCredits];
    DELETE FROM [purchases].[VendorCreditNumberCounters];
    DELETE FROM [purchases].[PaymentsMade];
    DELETE FROM [purchases].[PaymentMadeNumberCounters];
    DELETE FROM [purchases].[BillItems];
    DELETE FROM [purchases].[Bills];
    DELETE FROM [purchases].[BillNumberCounters];
    DELETE FROM [purchases].[PurchaseOrderItems];
    DELETE FROM [purchases].[PurchaseOrders];
    DELETE FROM [purchases].[PurchaseOrderNumberCounters];
    DELETE FROM [purchases].[VendorMaster];

    DELETE FROM [dbo].[ClientMaster];
    DELETE FROM [finance].[DailyExchangeRates];
    DELETE FROM [finance].[ExchangeRateChecks];

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
