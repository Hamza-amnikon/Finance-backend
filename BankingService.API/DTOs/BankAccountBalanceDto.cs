namespace BankingService.API.DTOs
{
    public class BankAccountBalanceDto
    {
        public int BankAccountId { get; set; }

        public string BankAccountName { get; set; } = "";

        public int CompanyId { get; set; }

        public string CompanyName { get; set; } = "";

        public string Currency { get; set; } = "";

        public int CurrencyDecimalPlaces { get; set; }

        public DateOnly? OpeningBalanceDate { get; set; }

        public bool IsOpeningBalanceConfigured { get; set; }

        public decimal? OpeningBalance { get; set; }

        public decimal? PostedMoneyIn { get; set; }

        public decimal? PostedMoneyOut { get; set; }

        public decimal? CurrentBalance { get; set; }
    }
}