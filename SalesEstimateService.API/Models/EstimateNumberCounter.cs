namespace SalesEstimateService.API.Models
{
    public class EstimateNumberCounter
    {
        public string PeriodKey { get; set; } = string.Empty;

        public int LastNumber { get; set; }
    }
}
