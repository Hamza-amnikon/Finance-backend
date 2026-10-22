using System.ComponentModel.DataAnnotations;

namespace BankingService.API.Services
{
    public class BankingExchangeRateOptions
    {
        [Required]
        public string BaseUrl { get; set; } = "";

        [Required]
        public string ProviderName { get; set; } = "";

        [Range(1, 120)]
        public int TimeoutSeconds { get; set; } = 15;

        [Range(1, 1440)]
        public int CacheMinutes { get; set; } = 30;
    }
}
