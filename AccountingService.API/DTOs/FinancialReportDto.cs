namespace AccountingService.API.DTOs
{
    public class FinancialReportAccountDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class FinancialReportDto
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public DateOnly FromDate { get; set; }
        public DateOnly ToDate { get; set; }
        public decimal Income { get; set; }
        public decimal Expenses { get; set; }
        public decimal Profit { get; set; }
        public decimal Assets { get; set; }
        public decimal Liabilities { get; set; }
        public decimal Equity { get; set; }
        public decimal UnclosedEarnings { get; set; }
        public decimal BalanceDifference { get; set; }
        public decimal CashOpening { get; set; }
        public decimal CashDebits { get; set; }
        public decimal CashCredits { get; set; }
        public decimal CashClosing { get; set; }
        public List<FinancialReportAccountDto> IncomeAccounts { get; set; } = [];
        public List<FinancialReportAccountDto> ExpenseAccounts { get; set; } = [];
        public List<FinancialReportAccountDto> AssetAccounts { get; set; } = [];
        public List<FinancialReportAccountDto> LiabilityAccounts { get; set; } = [];
        public List<FinancialReportAccountDto> EquityAccounts { get; set; } = [];
        public List<FinancialReportAccountDto> CashAccounts { get; set; } = [];
        public List<FinancialReportAccountDto> BankAccounts { get; set; } = [];
    }
}
