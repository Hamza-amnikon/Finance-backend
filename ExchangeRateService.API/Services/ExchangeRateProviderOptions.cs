using System.ComponentModel.DataAnnotations;

namespace ExchangeRateService.API.Services
{
    public class ExchangeRateProviderOptions
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
