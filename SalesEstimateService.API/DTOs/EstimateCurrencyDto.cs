namespace SalesEstimateService.API.DTOs
{
    public class EstimateCurrencyDto
    {
        public string CurrencyCode { get; set; } = string.Empty;

        public int DecimalPlaces { get; set; }

        public bool IsActive { get; set; }
    }
}
