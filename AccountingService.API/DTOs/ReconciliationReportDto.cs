namespace AccountingService.API.DTOs
{
    public class ReconciliationReportDto
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = "";
        public string Currency { get; set; } = "";
        public DateOnly AsOf { get; set; }
        public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;
        public List<ReconciliationCheckDto> Checks { get; set; } = [];
    }
    public class ReconciliationCheckDto
    {
        public string Id { get; set; } = "";
        public string Group { get; set; } = "";
        public string Title { get; set; } = "";
        public string Reference { get; set; } = "";
        public string Status { get; set; } = "";
        public string Currency { get; set; } = "";
        public decimal? Expected { get; set; }
        public decimal? Actual { get; set; }
        public decimal? Difference { get; set; }
        public string Explanation { get; set; } = "";
        public string? Link { get; set; }
    }

    public class ReconciliationDocumentRow
    {
        public int Id { get; set; }
        public string Kind { get; set; } = "";
        public string Number { get; set; } = "";
        public string Currency { get; set; } = "";
        public decimal Total { get; set; }
        public int PostingCount { get; set; }
        public int PostedCount { get; set; }
        public string? PostingCurrency { get; set; }
        public decimal? PostingTotal { get; set; }
    }

    public class ReconciliationBalanceRow
    {
        public string Kind { get; set; } = "";
        public decimal Balance { get; set; }
        public decimal? BookingRate { get; set; }
    }

    public class ReconciliationBankRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Currency { get; set; } = "";
        public int? AccountingAccountId { get; set; }
        public int MappingCount { get; set; }
        public int? LedgerCompanyId { get; set; }
        public string? LedgerType { get; set; }
        public bool? IsPostingAccount { get; set; }
        public decimal RegisterBalance { get; set; }
        public decimal LedgerBalance { get; set; }
        public DateOnly? LastCompletedDate { get; set; }
        public int UnmatchedStatementLines { get; set; }
    }

    public class ReconciliationJournalRow
    {
        public int Id { get; set; }
        public string Number { get; set; } = "";
        public string Currency { get; set; } = "";
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal LineDebit { get; set; }
        public decimal LineCredit { get; set; }
        public decimal BaseDebit { get; set; }
        public decimal BaseCredit { get; set; }
        public int LineCount { get; set; }
        public int OwnershipErrors { get; set; }
    }
}
