using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesEstimateService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddEstimateNumberCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EstimateNumberCounters",
                schema: "estimates",
                columns: table => new
                {
                    PeriodKey = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    LastNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstimateNumberCounters", x => x.PeriodKey);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EstimateNumberCounters",
                schema: "estimates");
        }
    }
}
