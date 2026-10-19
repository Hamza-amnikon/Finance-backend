using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesInvoiceService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesInvoiceIssuance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvoiceTemplateVersion",
                schema: "sales",
                table: "SalesInvoices",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IssuerSnapshotJson",
                schema: "sales",
                table: "SalesInvoices",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "InvoiceNumberCounters",
                schema: "sales",
                columns: table => new
                {
                    PeriodKey = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    LastNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceNumberCounters", x => x.PeriodKey);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceNumberCounters",
                schema: "sales");

            migrationBuilder.DropColumn(
                name: "InvoiceTemplateVersion",
                schema: "sales",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "IssuerSnapshotJson",
                schema: "sales",
                table: "SalesInvoices");
        }
    }
}
