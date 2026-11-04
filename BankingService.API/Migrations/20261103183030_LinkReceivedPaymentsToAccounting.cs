using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankingService.API.Migrations
{
    /// <inheritdoc />
    public partial class LinkReceivedPaymentsToAccounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountingJournalEntryId",
                schema: "banking",
                table: "PaymentBankingLinks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountingMessage",
                schema: "banking",
                table: "PaymentBankingLinks",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AccountingState",
                schema: "banking",
                table: "PaymentBankingLinks",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountingJournalEntryId",
                schema: "banking",
                table: "PaymentBankingLinks");

            migrationBuilder.DropColumn(
                name: "AccountingMessage",
                schema: "banking",
                table: "PaymentBankingLinks");

            migrationBuilder.DropColumn(
                name: "AccountingState",
                schema: "banking",
                table: "PaymentBankingLinks");
        }
    }
}
