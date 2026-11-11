using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PurchaseOrderService.API.Migrations
{
    /// <inheritdoc />
    public partial class LinkPurchaseOrderTaxSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TaxTreatment",
                schema: "purchases",
                table: "PurchaseOrders",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "NoTax");

            migrationBuilder.AddColumn<bool>(
                name: "UsesTaxSettings",
                schema: "purchases",
                table: "PurchaseOrders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TaxSettingId",
                schema: "purchases",
                table: "PurchaseOrderItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxSettingSnapshotJson",
                schema: "purchases",
                table: "PurchaseOrderItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaxTreatment",
                schema: "purchases",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "UsesTaxSettings",
                schema: "purchases",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "TaxSettingId",
                schema: "purchases",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "TaxSettingSnapshotJson",
                schema: "purchases",
                table: "PurchaseOrderItems");
        }
    }
}
