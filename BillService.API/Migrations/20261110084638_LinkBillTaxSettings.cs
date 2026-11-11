using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillService.API.Migrations
{
    /// <inheritdoc />
    public partial class LinkBillTaxSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UsesTaxSettings",
                schema: "purchases",
                table: "Bills",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TaxSettingId",
                schema: "purchases",
                table: "BillItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxSettingSnapshotJson",
                schema: "purchases",
                table: "BillItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UsesTaxSettings",
                schema: "purchases",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "TaxSettingId",
                schema: "purchases",
                table: "BillItems");

            migrationBuilder.DropColumn(
                name: "TaxSettingSnapshotJson",
                schema: "purchases",
                table: "BillItems");
        }
    }
}
