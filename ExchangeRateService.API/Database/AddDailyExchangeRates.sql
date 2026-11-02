-- Additive setup. Does not modify invoices, payments, expenses or their saved rates.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF SCHEMA_ID(N'finance') IS NULL EXEC(N'CREATE SCHEMA finance');
IF OBJECT_ID(N'finance.DailyExchangeRates', N'U') IS NULL
BEGIN
    CREATE TABLE finance.DailyExchangeRates (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_DailyExchangeRates PRIMARY KEY,
        CheckDate date NOT NULL,
        Currency nvarchar(3) NOT NULL,
        RateToInr decimal(28,12) NOT NULL,
        RateDate date NOT NULL,
        Source nvarchar(80) NOT NULL,
        RetrievedAtUtc datetimeoffset NOT NULL,
        CONSTRAINT UQ_DailyExchangeRates UNIQUE(CheckDate, Currency),
        CONSTRAINT CK_DailyExchangeRates_Positive CHECK(RateToInr > 0)
    );
END;
IF OBJECT_ID(N'finance.ExchangeRateChecks', N'U') IS NULL
BEGIN
    CREATE TABLE finance.ExchangeRateChecks (
        CheckDate date NOT NULL CONSTRAINT PK_ExchangeRateChecks PRIMARY KEY,
        CheckedAtUtc datetimeoffset NOT NULL,
        Status nvarchar(20) NOT NULL,
        Error nvarchar(1000) NULL
    );
END;
COMMIT;
