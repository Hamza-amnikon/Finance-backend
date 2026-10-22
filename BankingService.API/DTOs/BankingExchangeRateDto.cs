namespace BankingService.API.DTOs
{
    public class BankingExchangeRateDto
    {
        public string BaseCurrency { get; set; } = "";

        public string QuoteCurrency { get; set; } = "";

        public decimal Rate { get; set; }

        public string RateText { get; set; } = "";

        public DateOnly RateDate { get; set; }

        public string Source { get; set; } = "";

        public DateTimeOffset RetrievedAtUtc { get; set; }
    }
}
