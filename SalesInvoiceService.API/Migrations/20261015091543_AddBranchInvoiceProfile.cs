using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesInvoiceService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchInvoiceProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                schema: "sales",
                table: "BranchMasters",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankAccountName",
                schema: "sales",
                table: "BranchMasters",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankAccountNumber",
                schema: "sales",
                table: "BranchMasters",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankIban",
                schema: "sales",
                table: "BranchMasters",
                type: "nvarchar(34)",
                maxLength: 34,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                schema: "sales",
                table: "BranchMasters",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankSwiftCode",
                schema: "sales",
                table: "BranchMasters",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                schema: "sales",
                table: "BranchMasters",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompanyRegistrationNumber",
                schema: "sales",
                table: "BranchMasters",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                schema: "sales",
                table: "BranchMasters",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                schema: "sales",
                table: "BranchMasters",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNotes",
                schema: "sales",
                table: "BranchMasters",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                schema: "sales",
                table: "BranchMasters",
                type: "nvarchar(25)",
                maxLength: 25,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "sales",
                table: "BranchMasters",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "TaxRegistrationNumber",
                schema: "sales",
                table: "BranchMasters",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
                UPDATE [sales].[BranchMasters]
                SET [CompanyName] = N'AMNIKON Technologies FZCO',
                    [Address] = N'Building A1, Dubai Silicon Oasis, Dubai, United Arab Emirates',
                    [CountryCode] = N'AE',
                    [CompanyRegistrationNumber] = N'22091'
                WHERE [Name] = N'Dubai'
                  AND [CompanyName] = N'';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                schema: "sales",
                table: "BranchMasters");

            migrationBuilder.DropColumn(
                name: "BankAccountName",
                schema: "sales",
                table: "BranchMasters");

            migrationBuilder.DropColumn(
                name: "BankAccountNumber",
                schema: "sales",
                table: "BranchMasters");

            migrationBuilder.DropColumn(
                name: "BankIban",
                schema: "sales",
                table: "BranchMasters");

            migrationBuilder.DropColumn(
                name: "BankName",
                schema: "sales",
                table: "BranchMasters");

            migrationBuilder.DropColumn(
                name: "BankSwiftCode",
                schema: "sales",
                table: "BranchMasters");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                schema: "sales",
                table: "BranchMasters");

            migrationBuilder.DropColumn(
                name: "CompanyRegistrationNumber",
                schema: "sales",
                table: "BranchMasters");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                schema: "sales",
                table: "BranchMasters");

            migrationBuilder.DropColumn(
                name: "Email",
                schema: "sales",
                table: "BranchMasters");

            migrationBuilder.DropColumn(
                name: "InvoiceNotes",
                schema: "sales",
                table: "BranchMasters");

            migrationBuilder.DropColumn(
                name: "Phone",
                schema: "sales",
                table: "BranchMasters");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "sales",
                table: "BranchMasters");

            migrationBuilder.DropColumn(
                name: "TaxRegistrationNumber",
                schema: "sales",
                table: "BranchMasters");
        }
    }
}
