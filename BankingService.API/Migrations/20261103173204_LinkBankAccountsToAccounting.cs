using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankingService.API.Migrations
{
    /// <inheritdoc />
    public partial class LinkBankAccountsToAccounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountingAccountId",
                schema: "expenses",
                table: "BankAccounts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_CompanyId_AccountingAccountId",
                schema: "expenses",
                table: "BankAccounts",
                columns: new[] { "CompanyId", "AccountingAccountId" },
                unique: true,
                filter: "[AccountingAccountId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BankAccounts_CompanyId_AccountingAccountId",
                schema: "expenses",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "AccountingAccountId",
                schema: "expenses",
                table: "BankAccounts");
        }
    }
}
