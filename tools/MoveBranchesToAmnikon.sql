BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [sales].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112114128_MoveBranchesToAmnikon'
)
BEGIN
    IF SCHEMA_ID(N'amnikon') IS NULL EXEC(N'CREATE SCHEMA [amnikon];');
END;

IF NOT EXISTS (
    SELECT * FROM [sales].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112114128_MoveBranchesToAmnikon'
)
BEGIN
    EXEC sp_rename N'[sales].[BranchMasters]', N'Branches', 'OBJECT';
    ALTER SCHEMA [amnikon] TRANSFER [sales].[Branches];
END;

IF NOT EXISTS (
    SELECT * FROM [sales].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112114128_MoveBranchesToAmnikon'
)
BEGIN
    EXEC sp_rename N'[amnikon].[Branches].[IX_BranchMasters_Name]', N'IX_Branches_Name', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [sales].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112114128_MoveBranchesToAmnikon'
)
BEGIN
    CREATE SYNONYM [sales].[BranchMasters] FOR [amnikon].[Branches];
END;

IF NOT EXISTS (
    SELECT * FROM [sales].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112114128_MoveBranchesToAmnikon'
)
BEGIN
    INSERT INTO [sales].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112114128_MoveBranchesToAmnikon', N'10.0.12');
END;

COMMIT;
GO

