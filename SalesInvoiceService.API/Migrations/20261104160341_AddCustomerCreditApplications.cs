using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesInvoiceService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerCreditApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerCreditApplications",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceInvoiceId = table.Column<int>(type: "int", nullable: false),
                    TargetInvoiceId = table.Column<int>(type: "int", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Branch = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AppliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerCreditApplications", x => x.Id);
                    table.CheckConstraint("CK_CustomerCreditApplications_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_CustomerCreditApplications_Invoices", "[SourceInvoiceId] <> [TargetInvoiceId]");
                    table.ForeignKey(
                        name: "FK_CustomerCreditApplications_SalesInvoices_SourceInvoiceId",
                        column: x => x.SourceInvoiceId,
                        principalSchema: "sales",
                        principalTable: "SalesInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerCreditApplications_SalesInvoices_TargetInvoiceId",
                        column: x => x.TargetInvoiceId,
                        principalSchema: "sales",
                        principalTable: "SalesInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCreditApplications_RequestId",
                schema: "sales",
                table: "CustomerCreditApplications",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCreditApplications_SourceInvoiceId_AppliedAtUtc",
                schema: "sales",
                table: "CustomerCreditApplications",
                columns: new[] { "SourceInvoiceId", "AppliedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCreditApplications_TargetInvoiceId_AppliedAtUtc",
                schema: "sales",
                table: "CustomerCreditApplications",
                columns: new[] { "TargetInvoiceId", "AppliedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerCreditApplications",
                schema: "sales");
        }
    }
}
