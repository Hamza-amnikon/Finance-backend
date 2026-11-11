using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesInvoiceService.API.Migrations
{
    /// <inheritdoc />
    public partial class LinkInvoiceTaxSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UsesTaxSettings",
                schema: "sales",
                table: "SalesInvoices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TaxSettingId",
                schema: "sales",
                table: "SalesInvoiceItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxSettingSnapshotJson",
                schema: "sales",
                table: "SalesInvoiceItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UsesTaxSettings",
                schema: "sales",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "TaxSettingId",
                schema: "sales",
                table: "SalesInvoiceItems");

            migrationBuilder.DropColumn(
                name: "TaxSettingSnapshotJson",
                schema: "sales",
                table: "SalesInvoiceItems");
        }
    }
}
