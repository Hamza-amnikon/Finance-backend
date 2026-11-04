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

IF EXISTS (SELECT 1 FROM [accounting].[JournalEntryLines]) OR
   EXISTS (SELECT 1 FROM [accounting].[JournalEntries])
    THROW 50002, 'Journal records must be cleared before accounts.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    WHILE EXISTS (SELECT 1 FROM [accounting].[Accounts])
    BEGIN
        DELETE account
        FROM [accounting].[Accounts] AS account
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM [accounting].[Accounts] AS child
            WHERE child.[ParentAccountId] = account.[Id]
              AND child.[CompanyId] = account.[CompanyId]
        );

        IF @@ROWCOUNT = 0
            THROW 50003, 'The account hierarchy could not be cleared.', 1;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
