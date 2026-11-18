BEGIN TRANSACTION;
ALTER TABLE [security].[RolePermissions] ADD [CompanyId] int NULL;

INSERT INTO [security].[__PermissionMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261117083111_AddRolePermissionCompanyId', N'10.0.12');

COMMIT;
GO

