using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PurchaseOrderService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseOrderPaymentMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaymentModeId",
                schema: "purchases",
                table: "PurchaseOrders",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentModeId",
                schema: "purchases",
                table: "PurchaseOrders");
        }
    }
}
