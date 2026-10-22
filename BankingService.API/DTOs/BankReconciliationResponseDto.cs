namespace BankingService.API.DTOs
{
    public class BankReconciliationResponseDto
    {
        public int Id { get; set; }

        public Guid RequestId { get; set; }

        public int BankAccountId { get; set; }

        public string BankAccountName { get; set; } = "";

        public int CompanyId { get; set; }

        public string CompanyName { get; set; } = "";

        public string Currency { get; set; } = "";

        public int CurrencyDecimalPlaces { get; set; }

        public DateOnly PeriodStart { get; set; }

        public DateOnly PeriodEnd { get; set; }

        public decimal StatementOpeningBalance { get; set; }

        public decimal StatementClosingBalance { get; set; }

        public decimal StatementCalculatedClosingBalance { get; set; }

        public decimal StatementBalanceDifference { get; set; }

        public decimal BookBalance { get; set; }

        public decimal OutstandingDeposits { get; set; }

        public decimal OutstandingWithdrawals { get; set; }

        public decimal AdjustedStatementBalance { get; set; }

        public decimal Difference { get; set; }

        public int UnmatchedStatementLineCount { get; set; }

        public string Status { get; set; } = "";

        public string Notes { get; set; } = "";

        public DateTime CreatedDate { get; set; }

        public DateTime? ModifiedDate { get; set; }

        public DateTime? CompletedDate { get; set; }

        public byte[] RowVersion { get; set; } = [];

        public List<BankStatementLineResponseDto> Lines { get; set; } = [];

        public List<BankTransactionResponseDto> Books { get; set; } = [];
    }
}