using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionService.API.Migrations
{
    /// <inheritdoc />
    public partial class ScopeRolePermissionsByCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_RoleId_PermissionId",
                schema: "security",
                table: "RolePermissions");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_CompanyId_PermissionId",
                schema: "security",
                table: "RolePermissions",
                columns: new[] { "RoleId", "CompanyId", "PermissionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_RoleId_CompanyId_PermissionId",
                schema: "security",
                table: "RolePermissions");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_PermissionId",
                schema: "security",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);
        }
    }
}
