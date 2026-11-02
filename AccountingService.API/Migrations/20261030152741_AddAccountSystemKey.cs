using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountSystemKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SystemKey",
                schema: "accounting",
                table: "Accounts",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_CompanyId_SystemKey",
                schema: "accounting",
                table: "Accounts",
                columns: new[] { "CompanyId", "SystemKey" },
                unique: true,
                filter: "[SystemKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Accounts_CompanyId_SystemKey",
                schema: "accounting",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "SystemKey",
                schema: "accounting",
                table: "Accounts");
        }
    }
}
