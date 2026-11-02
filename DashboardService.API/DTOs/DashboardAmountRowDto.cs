using Microsoft.EntityFrameworkCore;

namespace DashboardService.API.DTOs
{
    public class DashboardAmountRowDto
    {
        public string Metric { get; set; } = "";

        [Precision(28, 8)]
        public decimal Amount { get; set; }

        public string Currency { get; set; } = "";
    }
}
