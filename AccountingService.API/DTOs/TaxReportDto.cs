namespace AccountingService.API.DTOs
{
    public class TaxReportAccountDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string SystemKey { get; set; } = string.Empty;
        public decimal OpeningBalance { get; set; }
        public decimal PeriodDebit { get; set; }
        public decimal PeriodCredit { get; set; }
        public decimal ClosingBalance { get; set; }
    }

    public class TaxReportDto
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public DateOnly FromDate { get; set; }
        public DateOnly ToDate { get; set; }
        public List<TaxReportAccountDto> Accounts { get; set; } = [];
    }
}
