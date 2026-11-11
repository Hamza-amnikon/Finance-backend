SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF SCHEMA_ID(N'accounting') IS NULL EXEC(N'CREATE SCHEMA accounting');
IF OBJECT_ID(N'accounting.TaxTypes', N'U') IS NULL
BEGIN
    CREATE TABLE accounting.TaxTypes
    (
        Id int IDENTITY PRIMARY KEY,
        Code nvarchar(30) NOT NULL,
        Name nvarchar(100) NOT NULL,
        CountryCode nvarchar(2) NOT NULL,
        Treatment nvarchar(40) NOT NULL,
        ComponentNamesJson nvarchar(max) NOT NULL CHECK (ISJSON(ComponentNamesJson)=1),
        IsActive bit NOT NULL,
        CONSTRAINT UQ_TaxTypes_Country_Code UNIQUE(CountryCode,Code)
    );
END;
IF OBJECT_ID(N'accounting.TaxRates', N'U') IS NULL
BEGIN
    CREATE TABLE accounting.TaxRates
    (
        Id int IDENTITY PRIMARY KEY,
        CompanyId int NOT NULL REFERENCES expenses.Companies(Id),
        TaxTypeId int NOT NULL REFERENCES accounting.TaxTypes(Id),
        Code nvarchar(30) NOT NULL,
        Name nvarchar(100) NOT NULL,
        Rate decimal(9,4) NOT NULL CHECK(Rate>=0 AND Rate<=100),
        EffectiveFrom date NOT NULL,
        EffectiveTo date NULL,
        ComponentsJson nvarchar(max) NOT NULL CHECK(ISJSON(ComponentsJson)=1),
        IsActive bit NOT NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT CK_TaxRates_Dates CHECK(EffectiveTo IS NULL OR EffectiveTo>=EffectiveFrom),
        CONSTRAINT UQ_TaxRates_Version UNIQUE(CompanyId,Code,EffectiveFrom)
    );
END;
-- Supported treatment definitions only. Business rates are entered by the administrator.
INSERT INTO accounting.TaxTypes(Code,Name,CountryCode,Treatment,ComponentNamesJson,IsActive)
SELECT v.Code,v.Name,v.CountryCode,v.Treatment,v.Components,1
FROM (VALUES
    (N'VAT',N'VAT',N'AE',N'UaeVat',N'["VAT"]'),
    (N'GST_LOCAL',N'CGST + SGST',N'IN',N'IndiaCgstSgst',N'["CGST","SGST"]'),
    (N'GST_INTER',N'IGST',N'IN',N'IndiaIgst',N'["IGST"]')
) v(Code,Name,CountryCode,Treatment,Components)
WHERE NOT EXISTS(SELECT 1 FROM accounting.TaxTypes t WHERE t.CountryCode=v.CountryCode AND t.Code=v.Code);
COMMIT;
