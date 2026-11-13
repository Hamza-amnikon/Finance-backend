using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionService.API.Migrations
{
    /// <inheritdoc />
    public partial class CreatePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "security");

            migrationBuilder.CreateTable(
                name: "Permissions",
                schema: "security",
                columns: table => new
                {
                    PermissionId = table.Column<int>(
                        type: "int",
                        nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PermissionCode = table.Column<string>(
                        type: "nvarchar(100)",
                        maxLength: 100,
                        nullable: false),
                    Module = table.Column<string>(
                        type: "nvarchar(100)",
                        maxLength: 100,
                        nullable: false),
                    PageName = table.Column<string>(
                        type: "nvarchar(100)",
                        maxLength: 100,
                        nullable: false),
                    Route = table.Column<string>(
                        type: "nvarchar(200)",
                        maxLength: 200,
                        nullable: false),
                    Status = table.Column<string>(
                        type: "nvarchar(20)",
                        maxLength: 20,
                        nullable: false),
                    CreatedDate = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: false),
                    ModifiedDate = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: true),
                    RowVersion = table.Column<byte[]>(
                        type: "rowversion",
                        rowVersion: true,
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        name: "PK_Permissions",
                        columns: row => row.PermissionId);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                schema: "security",
                columns: table => new
                {
                    RolePermissionId = table.Column<int>(
                        type: "int",
                        nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(
                        type: "int",
                        nullable: false),
                    PermissionId = table.Column<int>(
                        type: "int",
                        nullable: false),
                    CanView = table.Column<bool>(
                        type: "bit",
                        nullable: false),
                    CanCreate = table.Column<bool>(
                        type: "bit",
                        nullable: false),
                    CanEdit = table.Column<bool>(
                        type: "bit",
                        nullable: false),
                    CanDelete = table.Column<bool>(
                        type: "bit",
                        nullable: false),
                    CanApprove = table.Column<bool>(
                        type: "bit",
                        nullable: false),
                    CanExport = table.Column<bool>(
                        type: "bit",
                        nullable: false),
                    CanIssue = table.Column<bool>(
                        type: "bit",
                        nullable: false),
                    CanPost = table.Column<bool>(
                        type: "bit",
                        nullable: false),
                    CanVoid = table.Column<bool>(
                        type: "bit",
                        nullable: false),
                    CanReverse = table.Column<bool>(
                        type: "bit",
                        nullable: false),
                    CanReconcile = table.Column<bool>(
                        type: "bit",
                        nullable: false),
                    CreatedDate = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: false),
                    ModifiedDate = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: true),
                    RowVersion = table.Column<byte[]>(
                        type: "rowversion",
                        rowVersion: true,
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        name: "PK_RolePermissions",
                        columns: row => row.RolePermissionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_PermissionCode",
                schema: "security",
                table: "Permissions",
                column: "PermissionCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_PermissionId",
                schema: "security",
                table: "RolePermissions",
                columns: new[]
                {
                    "RoleId",
                    "PermissionId"
                },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Permissions",
                schema: "security");

            migrationBuilder.DropTable(
                name: "RolePermissions",
                schema: "security");
        }
    }
}
