using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PermissionService.API.Data;

namespace PermissionService.API.Migrations
{
    [DbContext(typeof(PermissionDbContext))]
    [Migration("20261123160000_AddUsersPagePermissionCatalog")]
    public class AddUsersPagePermissionCatalog : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM [security].[Permissions]
                    WHERE [PermissionCode] = N'settings.users'
                )
                BEGIN
                    INSERT INTO [security].[Permissions]
                        ([PermissionCode], [Module], [PageName], [Route], [Status], [CreatedDate])
                    VALUES
                        (N'settings.users', N'Settings', N'Users', N'/settings/users', N'Active', SYSUTCDATETIME());
                END;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Keep existing grants and any catalog row that predates this migration.
        }
    }
}
