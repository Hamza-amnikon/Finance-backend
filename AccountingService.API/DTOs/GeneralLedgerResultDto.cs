namespace AccountingService.API.DTOs
{
    public class GeneralLedgerResultDto
    {
        public int CompanyId { get; set; }

        public string CompanyName { get; set; }
            = string.Empty;

        public string BaseCurrency { get; set; }
            = string.Empty;

        public int AccountId { get; set; }

        public string AccountCode { get; set; }
            = string.Empty;

        public string AccountName { get; set; }
            = string.Empty;

        public string AccountType { get; set; }
            = string.Empty;

        public string NormalBalance { get; set; }
            = string.Empty;

        public DateOnly FromDate { get; set; }

        public DateOnly ToDate { get; set; }

        public string? Branch { get; set; }

        public string? CostCenter { get; set; }

        public decimal OpeningBalance { get; set; }

        public string OpeningBalanceSide { get; set; }
            = string.Empty;

        public decimal PeriodDebit { get; set; }

        public decimal PeriodCredit { get; set; }

        public decimal ClosingBalance { get; set; }

        public string ClosingBalanceSide { get; set; }
            = string.Empty;

        public IReadOnlyList<GeneralLedgerLineDto> Data { get; set; } = [];

        public int Total { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }
}