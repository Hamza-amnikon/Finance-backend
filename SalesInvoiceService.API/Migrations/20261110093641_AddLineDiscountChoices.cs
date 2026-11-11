using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesInvoiceService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLineDiscountChoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscountType",
                schema: "sales",
                table: "SalesInvoiceItems",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Fixed");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountValue",
                schema: "sales",
                table: "SalesInvoiceItems",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountType",
                schema: "sales",
                table: "SalesInvoiceItems");

            migrationBuilder.DropColumn(
                name: "DiscountValue",
                schema: "sales",
                table: "SalesInvoiceItems");
        }
    }
}
