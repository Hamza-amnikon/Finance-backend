using SalesEstimateService.API.Models;

namespace SalesEstimateService.API.DTOs
{
    public class SalesEstimateItemResponseDto
    {
        public int Id { get; set; }

        public int LineNumber { get; set; }

        public string ServiceName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public ServiceUnit Unit { get; set; }

        public decimal Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public bool DiscountEnabled { get; set; } = false;

        public decimal DiscountAmount { get; set; }

        public decimal TaxRate { get; set; }

        public decimal LineSubTotal { get; set; }

        public decimal TaxAmount { get; set; }

        public decimal LineTotal { get; set; }
    }
}