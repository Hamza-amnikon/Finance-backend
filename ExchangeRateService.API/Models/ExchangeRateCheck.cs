namespace ExchangeRateService.API.Models
{
    public class ExchangeRateCheck
    {
        public DateOnly CheckDate { get; set; }
        public DateTimeOffset CheckedAtUtc { get; set; }
        public string Status { get; set; } = "Running";
        public string? Error { get; set; }
    }
}
