using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientService.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedClientFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClientMaster_Code",
                table: "ClientMaster");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "ClientMaster");

            migrationBuilder.DropColumn(
                name: "ContactPerson",
                table: "ClientMaster");

            migrationBuilder.DropColumn(
                name: "PaymentTerms",
                table: "ClientMaster");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "ClientMaster",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactPerson",
                table: "ClientMaster",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PaymentTerms",
                table: "ClientMaster",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ClientMaster_Code",
                table: "ClientMaster",
                column: "Code",
                unique: true);
        }
    }
}
