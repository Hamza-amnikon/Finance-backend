using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddBillAccountingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TaxTreatment",
                schema: "purchases",
                table: "Bills",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountCode",
                schema: "purchases",
                table: "BillItems",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaxTreatment",
                schema: "purchases",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "AccountCode",
                schema: "purchases",
                table: "BillItems");
        }
    }
}
