using System.ComponentModel.DataAnnotations;

namespace SalesInvoiceService.API.Models
{
    public class SalesCreditNoteItem
    {
        public int Id { get; set; }

        public int SalesCreditNoteId { get; set; }

        public SalesCreditNote SalesCreditNote { get; set; } = null!;

        public int SalesInvoiceItemId { get; set; }

        public int LineNumber { get; set; }

        [Required]
        [MaxLength(200)]
        public string ServiceName { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        public ServiceUnit Unit { get; set; }
            = ServiceUnit.FixedFee;

        public decimal Quantity { get; set; }

        // Net credit before tax; null retains the original quantity-based mode.
        public decimal? CreditAmount { get; set; }

        public decimal UnitPrice { get; set; }

        public bool DiscountEnabled { get; set; }

        public decimal DiscountAmount { get; set; }

        public decimal TaxRate { get; set; }

        public decimal LineSubTotal { get; set; }

        public decimal TaxAmount { get; set; }

        public decimal LineTotal { get; set; }
    }
}