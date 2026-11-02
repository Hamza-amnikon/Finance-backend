namespace DashboardService.API.DTOs
{
    public class DashboardSummaryDto
    {
        public string DataMode { get; set; } = "live";

        public DateOnly From { get; set; }

        public DateOnly To { get; set; }

        public string Branch { get; set; } = "All branches";

        public string Currency { get; set; } = "INR";

        public decimal Revenue { get; set; }

        public decimal Expenses { get; set; }

        public decimal Received { get; set; }

        public decimal Paid { get; set; }

        public decimal AccountsReceivable { get; set; }

        public decimal CustomerCredits { get; set; }

        public decimal AccountsPayable { get; set; }

        public int Customers { get; set; }

        public int Vendors { get; set; }

        public decimal RateToInr { get; set; } = 1m;

        public string RateDate { get; set; } = "INR base currency";

        public string RateSource { get; set; } = "Same currency";

        public DateTimeOffset? RatePublishedAt { get; set; }

        public string? RateWarning { get; set; }
    }
}
