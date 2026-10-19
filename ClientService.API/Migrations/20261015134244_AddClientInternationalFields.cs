using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddClientInternationalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Ein",
                table: "ClientMaster",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Iban",
                table: "ClientMaster",
                type: "nvarchar(34)",
                maxLength: 34,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsVatRegistered",
                table: "ClientMaster",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoutingNumber",
                table: "ClientMaster",
                type: "nvarchar(9)",
                maxLength: 9,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SwiftCode",
                table: "ClientMaster",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VatTrn",
                table: "ClientMaster",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Ein",
                table: "ClientMaster");

            migrationBuilder.DropColumn(
                name: "Iban",
                table: "ClientMaster");

            migrationBuilder.DropColumn(
                name: "IsVatRegistered",
                table: "ClientMaster");

            migrationBuilder.DropColumn(
                name: "RoutingNumber",
                table: "ClientMaster");

            migrationBuilder.DropColumn(
                name: "SwiftCode",
                table: "ClientMaster");

            migrationBuilder.DropColumn(
                name: "VatTrn",
                table: "ClientMaster");
        }
    }
}
