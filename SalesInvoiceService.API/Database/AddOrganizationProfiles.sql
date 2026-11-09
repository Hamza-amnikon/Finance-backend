SET XACT_ABORT ON;
IF OBJECT_ID(N'sales.OrganizationProfiles', N'U') IS NULL
BEGIN
    CREATE TABLE sales.OrganizationProfiles
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrganizationProfiles PRIMARY KEY,
        Name nvarchar(200) NOT NULL,
        Email nvarchar(254) NOT NULL,
        Phone nvarchar(25) NOT NULL,
        Currency nvarchar(3) NOT NULL,
        Address nvarchar(1000) NOT NULL,
        Notes nvarchar(2000) NOT NULL
    );
END;
