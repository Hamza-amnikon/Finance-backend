using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestCancelPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanCancel",
                schema: "security",
                table: "RolePermissions",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanCancel",
                schema: "security",
                table: "RolePermissions");
        }
    }
}
