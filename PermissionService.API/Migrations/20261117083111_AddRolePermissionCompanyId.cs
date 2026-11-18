using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRolePermissionCompanyId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                schema: "security",
                table: "RolePermissions",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyId",
                schema: "security",
                table: "RolePermissions");
        }
    }
}
