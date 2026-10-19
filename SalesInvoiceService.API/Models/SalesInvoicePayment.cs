using System.ComponentModel.DataAnnotations;

namespace SalesInvoiceService.API.Models
{
    public class SalesInvoicePayment
    {
        public int Id { get; set; }

        public int SalesInvoiceId { get; set; }

        // Identifies one submission to prevent duplicate payments on retry.
        public Guid RequestId { get; set; }

        public DateOnly PaymentDate { get; set; }

        public decimal Amount { get; set; }

        [Required]
        [MaxLength(3)]
        public string Currency { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string PaymentMode { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Reference { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string Notes { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public SalesInvoice SalesInvoice { get; set; } = null!;
    }
}