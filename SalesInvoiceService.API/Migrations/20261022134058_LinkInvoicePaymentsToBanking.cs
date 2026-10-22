using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesInvoiceService.API.Migrations
{
    /// <inheritdoc />
    public partial class LinkInvoicePaymentsToBanking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BankAccountId",
                schema: "sales",
                table: "SalesInvoicePayments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountName",
                schema: "sales",
                table: "SalesInvoicePayments",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BankAmount",
                schema: "sales",
                table: "SalesInvoicePayments",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankCurrency",
                schema: "sales",
                table: "SalesInvoicePayments",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BankTransactionId",
                schema: "sales",
                table: "SalesInvoicePayments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementType",
                schema: "sales",
                table: "SalesInvoicePayments",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoicePayments_BankTransactionId",
                schema: "sales",
                table: "SalesInvoicePayments",
                column: "BankTransactionId",
                unique: true,
                filter: "[BankTransactionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesInvoicePayments_BankTransactionId",
                schema: "sales",
                table: "SalesInvoicePayments");

            migrationBuilder.DropColumn(
                name: "BankAccountId",
                schema: "sales",
                table: "SalesInvoicePayments");

            migrationBuilder.DropColumn(
                name: "BankAccountName",
                schema: "sales",
                table: "SalesInvoicePayments");

            migrationBuilder.DropColumn(
                name: "BankAmount",
                schema: "sales",
                table: "SalesInvoicePayments");

            migrationBuilder.DropColumn(
                name: "BankCurrency",
                schema: "sales",
                table: "SalesInvoicePayments");

            migrationBuilder.DropColumn(
                name: "BankTransactionId",
                schema: "sales",
                table: "SalesInvoicePayments");

            migrationBuilder.DropColumn(
                name: "SettlementType",
                schema: "sales",
                table: "SalesInvoicePayments");
        }
    }
}
