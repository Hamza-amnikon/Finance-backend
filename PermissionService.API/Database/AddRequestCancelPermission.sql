BEGIN TRANSACTION;
ALTER TABLE [security].[RolePermissions] ADD [CanCancel] bit NOT NULL DEFAULT CAST(0 AS bit);

INSERT INTO [security].[__PermissionMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261120102838_AddRequestCancelPermission', N'10.0.12');

COMMIT;
GO

