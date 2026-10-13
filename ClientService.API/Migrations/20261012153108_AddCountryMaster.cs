using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCountryMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "countrymasters",
                columns: table => new
                {
                    CountryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CountryName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LocationCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_countrymasters", x => x.CountryId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_countrymasters_CountryName_State_City",
                table: "countrymasters",
                columns: new[] { "CountryName", "State", "City" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "countrymasters");
        }
    }
}
