namespace DashboardService.API.Services
{
    public class DashboardExchangeRateDto
    {
        public decimal Rate { get; set; }

        public DateOnly RateDate { get; set; }

        public string Source { get; set; } = "";

        public DateTimeOffset RetrievedAtUtc { get; set; }
    }
}
