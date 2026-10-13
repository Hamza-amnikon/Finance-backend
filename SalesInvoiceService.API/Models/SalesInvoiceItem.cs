using System.ComponentModel.DataAnnotations;

namespace SalesInvoiceService.API.Models
{
    public enum ServiceUnit
    {
        Hour = 0,
        Day = 1,
        Month = 2,
        FixedFee = 3
    }

    public class SalesInvoiceItem
    {
        [Key]
        public int Id { get; set; }

        public int SalesInvoiceId { get; set; }

        public SalesInvoice SalesInvoice { get; set; } = null!;

        public int LineNumber { get; set; }

        [Required]
        [MaxLength(200)]
        public string ServiceName { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        public ServiceUnit Unit { get; set; } =
            ServiceUnit.FixedFee;

        public decimal Quantity { get; set; } = 1;

        public decimal UnitPrice { get; set; }

        // Monetary discount, not a percentage.
        public decimal DiscountAmount { get; set; }

        // Percentage: 5 means 5%.
        public decimal TaxRate { get; set; }

        // Calculated by the backend.
        public decimal LineSubTotal { get; set; }

        public decimal TaxAmount { get; set; }

        public decimal LineTotal { get; set; }
    }
}