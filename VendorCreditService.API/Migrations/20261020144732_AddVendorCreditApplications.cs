using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VendorCreditService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorCreditApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VendorCreditApplications",
                schema: "purchases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    VendorCreditId = table.Column<int>(type: "int", nullable: false),
                    VendorCreditNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BillId = table.Column<int>(type: "int", nullable: false),
                    BillNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ApplicationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VoidedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorCreditApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorCreditApplications_VendorCredits_VendorCreditId",
                        column: x => x.VendorCreditId,
                        principalSchema: "purchases",
                        principalTable: "VendorCredits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VendorCreditApplications_BillId_Status",
                schema: "purchases",
                table: "VendorCreditApplications",
                columns: new[] { "BillId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorCreditApplications_RequestId",
                schema: "purchases",
                table: "VendorCreditApplications",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorCreditApplications_VendorCreditId_Status",
                schema: "purchases",
                table: "VendorCreditApplications",
                columns: new[] { "VendorCreditId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VendorCreditApplications",
                schema: "purchases");
        }
    }
}
