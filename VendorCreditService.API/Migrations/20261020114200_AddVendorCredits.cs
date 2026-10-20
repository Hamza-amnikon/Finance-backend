using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VendorCreditService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "purchases");

            migrationBuilder.CreateTable(
                name: "VendorCreditNumberCounters",
                schema: "purchases",
                columns: table => new
                {
                    PeriodKey = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    LastNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorCreditNumberCounters", x => x.PeriodKey);
                });

            migrationBuilder.CreateTable(
                name: "VendorCredits",
                schema: "purchases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillId = table.Column<int>(type: "int", nullable: true),
                    VendorCreditNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VendorCreditNoteNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VendorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BillingAddress = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreditDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Branch = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CurrencyDecimalPlaces = table.Column<int>(type: "int", nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DiscountTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TaxTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    GrandTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IssuedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorCredits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VendorCreditItems",
                schema: "purchases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VendorCreditId = table.Column<int>(type: "int", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DiscountEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    LineSubTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorCreditItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorCreditItems_VendorCredits_VendorCreditId",
                        column: x => x.VendorCreditId,
                        principalSchema: "purchases",
                        principalTable: "VendorCredits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VendorCreditItems_VendorCreditId_LineNumber",
                schema: "purchases",
                table: "VendorCreditItems",
                columns: new[] { "VendorCreditId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorCredits_BillId",
                schema: "purchases",
                table: "VendorCredits",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorCredits_RequestId",
                schema: "purchases",
                table: "VendorCredits",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorCredits_VendorCreditNumber",
                schema: "purchases",
                table: "VendorCredits",
                column: "VendorCreditNumber",
                unique: true,
                filter: "[VendorCreditNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VendorCredits_VendorId",
                schema: "purchases",
                table: "VendorCredits",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorCredits_VendorId_VendorCreditNoteNumber",
                schema: "purchases",
                table: "VendorCredits",
                columns: new[] { "VendorId", "VendorCreditNoteNumber" },
                unique: true,
                filter: "[VendorCreditNoteNumber] <> N'' AND [Status] <> N'Cancelled'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VendorCreditItems",
                schema: "purchases");

            migrationBuilder.DropTable(
                name: "VendorCreditNumberCounters",
                schema: "purchases");

            migrationBuilder.DropTable(
                name: "VendorCredits",
                schema: "purchases");
        }
    }
}
