BEGIN TRANSACTION;
CREATE TABLE [security].[Users] (
    [UserId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [ObjectId] uniqueidentifier NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [Email] nvarchar(254) NOT NULL,
    [RoleId] int NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    [ModifiedDate] datetime2 NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([UserId]),
    CONSTRAINT [FK_Users_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [security].[Roles] ([RoleId]) ON DELETE NO ACTION
);

CREATE INDEX [IX_Users_RoleId] ON [security].[Users] ([RoleId]);

CREATE UNIQUE INDEX [IX_Users_TenantId_ObjectId] ON [security].[Users] ([TenantId], [ObjectId]);

INSERT INTO [security].[__RoleMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261117095552_CreateUsers', N'10.0.12');

COMMIT;
GO

