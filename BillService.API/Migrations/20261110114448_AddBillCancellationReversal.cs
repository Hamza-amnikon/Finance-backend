using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddBillCancellationReversal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AccountingReversalPending",
                schema: "purchases",
                table: "Bills",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountingReversalPending",
                schema: "purchases",
                table: "Bills");
        }
    }
}
