namespace ExpenseService.API.DTOs
{
    public class ExpenseResponseDto
    {
        public int Id { get; set; }

        public Guid RequestId { get; set; }

        public int CompanyId { get; set; }

        public string CompanyName { get; set; } = "";

        public int? BankAccountId { get; set; }

        public string? BankAccountName { get; set; }

        public DateOnly Date { get; set; }

        public string Supplier { get; set; } = "";

        public string Category { get; set; } = "";

        public string Notes { get; set; } = "";

        public decimal Amount { get; set; }

        public string Currency { get; set; } = "";

        public int CurrencyDecimalPlaces { get; set; }

        public decimal Rate { get; set; }

        public string RateBaseCurrency { get; set; } = "";

        public string RateQuoteCurrency { get; set; } = "";

        public DateOnly RateDate { get; set; }

        public string Status { get; set; } = "";

        public string Reference { get; set; } = "";

        public string VoidReason { get; set; } = "";

        public byte[] RowVersion { get; set; } = [];

        public string? ReceiptName { get; set; }

        public decimal? DisplayRate { get; set; }

        public string? DisplayRateBaseCurrency { get; set; }

        public string? DisplayRateQuoteCurrency { get; set; }

        public DateOnly? DisplayRateDate { get; set; }

        public string? DisplayRateSource { get; set; }

        public decimal? DisplayAmount { get; set; }

        public int DisplayCurrencyDecimalPlaces { get; set; }

        public string DisplayCurrency { get; set; } = "";
    }

    public class ExpenseListDto
    {
        public List<ExpenseResponseDto> Data { get; set; } = [];

        public int Total { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }

        public int DisplayCurrencyDecimalPlaces { get; set; }

        public int MissingRateCount { get; set; }

        public decimal? Draft { get; set; }

        public decimal? Recorded { get; set; }

        public decimal? Voided { get; set; }

        public string DisplayCurrency { get; set; } = "";
    }
}
