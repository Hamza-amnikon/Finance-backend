IF NOT EXISTS (
    SELECT 1 FROM security.Permissions WHERE PermissionCode = N'settings.users'
)
BEGIN
    INSERT INTO security.Permissions
        (PermissionCode, Module, PageName, Route, Status, CreatedDate)
    VALUES
        (N'settings.users', N'Settings', N'Users', N'/settings/users', N'Active', SYSUTCDATETIME());
END;
