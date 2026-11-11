using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesEstimateService.API.Migrations
{
    /// <inheritdoc />
    public partial class LinkEstimateTaxSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TaxTreatment",
                schema: "estimates",
                table: "SalesEstimates",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "NoTax");

            migrationBuilder.AddColumn<bool>(
                name: "UsesTaxSettings",
                schema: "estimates",
                table: "SalesEstimates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TaxSettingId",
                schema: "estimates",
                table: "SalesEstimateItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxSettingSnapshotJson",
                schema: "estimates",
                table: "SalesEstimateItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaxTreatment",
                schema: "estimates",
                table: "SalesEstimates");

            migrationBuilder.DropColumn(
                name: "UsesTaxSettings",
                schema: "estimates",
                table: "SalesEstimates");

            migrationBuilder.DropColumn(
                name: "TaxSettingId",
                schema: "estimates",
                table: "SalesEstimateItems");

            migrationBuilder.DropColumn(
                name: "TaxSettingSnapshotJson",
                schema: "estimates",
                table: "SalesEstimateItems");
        }
    }
}
