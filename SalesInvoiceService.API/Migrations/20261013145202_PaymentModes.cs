using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesInvoiceService.API.Migrations
{
    /// <inheritdoc />
    public partial class PaymentModes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentModes",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentModes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentModes_Name",
                schema: "sales",
                table: "PaymentModes",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentModes",
                schema: "sales");
        }
    }
}
