using VendorCreditService.API.Models;

namespace VendorCreditService.API.DTOs
{
    public class VendorCreditItemResponseDto
    {
        public int Id { get; set; }

        public int LineNumber { get; set; }

        public string ItemName { get; set; } = "";

        public string Description { get; set; } = "";

        public PurchaseUnit Unit { get; set; }

        public decimal Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public bool DiscountEnabled { get; set; }

        public decimal DiscountAmount { get; set; }

        public decimal TaxRate { get; set; }

        public decimal LineSubTotal { get; set; }

        public decimal TaxAmount { get; set; }

        public decimal LineTotal { get; set; }
    }
}