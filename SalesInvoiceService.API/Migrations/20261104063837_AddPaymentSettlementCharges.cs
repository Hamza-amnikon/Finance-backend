using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesInvoiceService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentSettlementCharges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BankChargeAmount",
                schema: "sales",
                table: "SalesInvoicePayments",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankChargeAmount",
                schema: "sales",
                table: "SalesInvoicePayments");
        }
    }
}
