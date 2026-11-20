using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankingService.API.Migrations
{
    /// <inheritdoc />
    public partial class LinkInternalPaymentRequestTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InternalPaymentRequestTransfers",
                schema: "banking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InternalPaymentRequestId = table.Column<int>(type: "int", nullable: false),
                    BankTransferId = table.Column<int>(type: "int", nullable: false),
                    LinkedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LinkedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalPaymentRequestTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InternalPaymentRequestTransfers_BankTransfers_BankTransferId",
                        column: x => x.BankTransferId,
                        principalSchema: "banking",
                        principalTable: "BankTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InternalPaymentRequestTransfers_InternalPaymentRequests_InternalPaymentRequestId",
                        column: x => x.InternalPaymentRequestId,
                        principalSchema: "banking",
                        principalTable: "InternalPaymentRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InternalPaymentRequestTransfers_BankTransferId",
                schema: "banking",
                table: "InternalPaymentRequestTransfers",
                column: "BankTransferId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InternalPaymentRequestTransfers_InternalPaymentRequestId",
                schema: "banking",
                table: "InternalPaymentRequestTransfers",
                column: "InternalPaymentRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InternalPaymentRequestTransfers",
                schema: "banking");
        }
    }
}
