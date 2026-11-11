using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyTaxSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaxTypes",
                schema: "accounting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Treatment = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ComponentNamesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxRates",
                schema: "accounting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    TaxTypeId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    ComponentsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxRates_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "expenses",
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxRates_TaxTypes_TaxTypeId",
                        column: x => x.TaxTypeId,
                        principalSchema: "accounting",
                        principalTable: "TaxTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaxRates_CompanyId_Code_EffectiveFrom",
                schema: "accounting",
                table: "TaxRates",
                columns: new[] { "CompanyId", "Code", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxRates_TaxTypeId",
                schema: "accounting",
                table: "TaxRates",
                column: "TaxTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxTypes_CountryCode_Code",
                schema: "accounting",
                table: "TaxTypes",
                columns: new[] { "CountryCode", "Code" },
                unique: true);
            migrationBuilder.Sql("""
                INSERT INTO accounting.TaxTypes(Code,Name,CountryCode,Treatment,ComponentNamesJson,IsActive)
                SELECT v.Code,v.Name,v.CountryCode,v.Treatment,v.Components,1
                FROM (VALUES
                    (N'VAT',N'VAT',N'AE',N'UaeVat',N'["VAT"]'),
                    (N'GST_LOCAL',N'CGST + SGST',N'IN',N'IndiaCgstSgst',N'["CGST","SGST"]'),
                    (N'GST_INTER',N'IGST',N'IN',N'IndiaIgst',N'["IGST"]')
                ) v(Code,Name,CountryCode,Treatment,Components)
                WHERE NOT EXISTS(SELECT 1 FROM accounting.TaxTypes t WHERE t.CountryCode=v.CountryCode AND t.Code=v.Code);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaxRates",
                schema: "accounting");

            migrationBuilder.DropTable(
                name: "TaxTypes",
                schema: "accounting");
        }
    }
}
