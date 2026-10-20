namespace ExpenseService.API.Services
{
    public class ExchangeRateOptions
    {
        public string BaseUrl { get; set; } = "";

        public string ProviderName { get; set; } = "";

        public int TimeoutSeconds { get; set; } = 15;

        public int CacheMinutes { get; set; } = 30;
    }
}
