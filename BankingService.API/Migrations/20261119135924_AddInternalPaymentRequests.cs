using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankingService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddInternalPaymentRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM security.Permissions WHERE PermissionCode = N'banking.internal-payment-requests')
                BEGIN
                    INSERT INTO security.Permissions
                        (PermissionCode, Module, PageName, Route, Status, CreatedDate)
                    VALUES
                        (N'banking.internal-payment-requests', N'Banking', N'Internal Payment Requests',
                         N'/banking/internal-payment-requests', N'Active', SYSUTCDATETIME());
                END;
                """);

            migrationBuilder.CreateTable(
                name: "InternalPaymentRequests",
                schema: "banking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestingCompanyId = table.Column<int>(type: "int", nullable: false),
                    PayingCompanyId = table.Column<int>(type: "int", nullable: false),
                    ReceivingBankAccountId = table.Column<int>(type: "int", nullable: false),
                    RequestDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RequiredDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CurrencyDecimalPlaces = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DecisionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DecidedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalPaymentRequests", x => x.Id);
                    table.CheckConstraint("CK_InternalPaymentRequest_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_InternalPaymentRequest_Companies", "[RequestingCompanyId] <> [PayingCompanyId]");
                    table.CheckConstraint("CK_InternalPaymentRequest_Dates", "[RequiredDate] >= [RequestDate]");
                    table.CheckConstraint("CK_InternalPaymentRequest_Status", "[Status] IN ('Draft', 'Submitted', 'Approved', 'Rejected', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_InternalPaymentRequests_BankAccounts_ReceivingBankAccountId_RequestingCompanyId",
                        columns: x => new { x.ReceivingBankAccountId, x.RequestingCompanyId },
                        principalSchema: "expenses",
                        principalTable: "BankAccounts",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InternalPaymentRequestLines",
                schema: "banking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InternalPaymentRequestId = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalPaymentRequestLines", x => x.Id);
                    table.CheckConstraint("CK_InternalPaymentRequestLine_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_InternalPaymentRequestLines_InternalPaymentRequests_InternalPaymentRequestId",
                        column: x => x.InternalPaymentRequestId,
                        principalSchema: "banking",
                        principalTable: "InternalPaymentRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InternalPaymentRequestLines_InternalPaymentRequestId",
                schema: "banking",
                table: "InternalPaymentRequestLines",
                column: "InternalPaymentRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalPaymentRequests_PayingCompanyId_Status",
                schema: "banking",
                table: "InternalPaymentRequests",
                columns: new[] { "PayingCompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InternalPaymentRequests_ReceivingBankAccountId_RequestingCompanyId",
                schema: "banking",
                table: "InternalPaymentRequests",
                columns: new[] { "ReceivingBankAccountId", "RequestingCompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_InternalPaymentRequests_RequestId",
                schema: "banking",
                table: "InternalPaymentRequests",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InternalPaymentRequests_RequestingCompanyId_RequestDate",
                schema: "banking",
                table: "InternalPaymentRequests",
                columns: new[] { "RequestingCompanyId", "RequestDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InternalPaymentRequestLines",
                schema: "banking");

            migrationBuilder.DropTable(
                name: "InternalPaymentRequests",
                schema: "banking");
        }
    }
}
