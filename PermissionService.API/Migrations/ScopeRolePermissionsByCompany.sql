BEGIN TRANSACTION;
DROP INDEX [IX_RolePermissions_RoleId_PermissionId] ON [security].[RolePermissions];

CREATE UNIQUE INDEX [IX_RolePermissions_RoleId_CompanyId_PermissionId] ON [security].[RolePermissions] ([RoleId], [CompanyId], [PermissionId]);

INSERT INTO [security].[__PermissionMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261117083521_ScopeRolePermissionsByCompany', N'10.0.12');

COMMIT;
GO

