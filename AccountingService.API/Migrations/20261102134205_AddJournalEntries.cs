using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JournalEntries",
                schema: "accounting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    EntryNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Branch = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    Narration = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SourceDocumentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceDocumentNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalDebit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    TotalCredit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    ReversalOfJournalEntryId = table.Column<int>(type: "int", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntries", x => x.Id);
                    table.UniqueConstraint("AK_JournalEntries_Id_CompanyId", x => new { x.Id, x.CompanyId });
                    table.CheckConstraint("CK_JournalEntries_EntryNumber", "LEN(LTRIM(RTRIM([EntryNumber]))) > 0");
                    table.CheckConstraint("CK_JournalEntries_ExchangeRate", "[ExchangeRate] > 0");
                    table.CheckConstraint("CK_JournalEntries_PostedBalance", "[Status] = N'Draft' OR [TotalDebit] = [TotalCredit]");
                    table.CheckConstraint("CK_JournalEntries_Reversal", "[ReversalOfJournalEntryId] IS NULL OR [ReversalOfJournalEntryId] <> [Id]");
                    table.CheckConstraint("CK_JournalEntries_Totals", "[TotalDebit] >= 0 AND [TotalCredit] >= 0");
                    table.ForeignKey(
                        name: "FK_JournalEntries_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "expenses",
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalEntries_JournalEntries_ReversalOfJournalEntryId_CompanyId",
                        columns: x => new { x.ReversalOfJournalEntryId, x.CompanyId },
                        principalSchema: "accounting",
                        principalTable: "JournalEntries",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JournalEntryLines",
                schema: "accounting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JournalEntryId = table.Column<int>(type: "int", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DebitAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CreditAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    BaseDebitAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    BaseCreditAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CostCenter = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CounterpartyType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CounterpartyId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntryLines", x => x.Id);
                    table.CheckConstraint("CK_JournalEntryLines_Amount", "([DebitAmount] > 0 AND [CreditAmount] = 0) OR ([CreditAmount] > 0 AND [DebitAmount] = 0)");
                    table.CheckConstraint("CK_JournalEntryLines_BaseAmount", "([BaseDebitAmount] > 0 AND [BaseCreditAmount] = 0) OR ([BaseCreditAmount] > 0 AND [BaseDebitAmount] = 0)");
                    table.CheckConstraint("CK_JournalEntryLines_LineNumber", "[LineNumber] > 0");
                    table.ForeignKey(
                        name: "FK_JournalEntryLines_Accounts_AccountId_CompanyId",
                        columns: x => new { x.AccountId, x.CompanyId },
                        principalSchema: "accounting",
                        principalTable: "Accounts",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalEntryLines_JournalEntries_JournalEntryId_CompanyId",
                        columns: x => new { x.JournalEntryId, x.CompanyId },
                        principalSchema: "accounting",
                        principalTable: "JournalEntries",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_CompanyId_EntryDate_Status",
                schema: "accounting",
                table: "JournalEntries",
                columns: new[] { "CompanyId", "EntryDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_CompanyId_EntryNumber",
                schema: "accounting",
                table: "JournalEntries",
                columns: new[] { "CompanyId", "EntryNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_CompanyId_Source_SourceDocumentId",
                schema: "accounting",
                table: "JournalEntries",
                columns: new[] { "CompanyId", "Source", "SourceDocumentId" },
                unique: true,
                filter: "[SourceDocumentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_ReversalOfJournalEntryId_CompanyId",
                schema: "accounting",
                table: "JournalEntries",
                columns: new[] { "ReversalOfJournalEntryId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLines_AccountId_CompanyId",
                schema: "accounting",
                table: "JournalEntryLines",
                columns: new[] { "AccountId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLines_CompanyId_AccountId",
                schema: "accounting",
                table: "JournalEntryLines",
                columns: new[] { "CompanyId", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLines_JournalEntryId_CompanyId",
                schema: "accounting",
                table: "JournalEntryLines",
                columns: new[] { "JournalEntryId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLines_JournalEntryId_LineNumber",
                schema: "accounting",
                table: "JournalEntryLines",
                columns: new[] { "JournalEntryId", "LineNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JournalEntryLines",
                schema: "accounting");

            migrationBuilder.DropTable(
                name: "JournalEntries",
                schema: "accounting");
        }
    }
}
