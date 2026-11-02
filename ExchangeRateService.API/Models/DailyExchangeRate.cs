namespace ExchangeRateService.API.Models
{
    public class DailyExchangeRate
    {
        public int Id { get; set; }
        public DateOnly CheckDate { get; set; }
        public string Currency { get; set; } = "";
        public decimal RateToInr { get; set; }
        public DateOnly RateDate { get; set; }
        public string Source { get; set; } = "";
        public DateTimeOffset RetrievedAtUtc { get; set; }
    }
}
