using System.ComponentModel.DataAnnotations;

namespace SalesInvoiceService.API.Models
{
    public enum SalesCreditNoteStatus
    {
        Draft = 0,
        Issued = 1
    }

    public class SalesCreditNote
    {
        public int Id { get; set; }

        // Prevents duplicate creation when the same request is retried.
        public Guid RequestId { get; set; }

        // The original invoice being credited.
        public int SalesInvoiceId { get; set; }

        public SalesInvoice SalesInvoice { get; set; } = null!;

        // Assigned when the credit note is issued.
        [MaxLength(50)]
        public string? CreditNoteNumber { get; set; }

        public DateOnly CreditNoteDate { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string Notes { get; set; } = string.Empty;

        // Copied from the original invoice.
        [Required]
        [MaxLength(3)]
        public string Currency { get; set; } = string.Empty;

        public decimal SubTotal { get; set; }

        public decimal DiscountTotal { get; set; }

        public decimal TaxTotal { get; set; }

        public decimal GrandTotal { get; set; }

        public SalesCreditNoteStatus Status { get; set; }
            = SalesCreditNoteStatus.Draft;

        public DateTime CreatedDate { get; set; }
            = DateTime.UtcNow;

        public DateTime? IssuedDate { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];

        public ICollection<SalesCreditNoteItem> Items { get; set; } = new List<SalesCreditNoteItem>();
    }
}