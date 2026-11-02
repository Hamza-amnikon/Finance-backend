namespace ExchangeRateService.API.DTOs
{
    public class ExchangeRateDto
    {
        public decimal Rate { get; set; }

        public DateOnly RateDate { get; set; }

        public string Source { get; set; } = "";

        public DateTimeOffset RetrievedAtUtc { get; set; }
    }
}
