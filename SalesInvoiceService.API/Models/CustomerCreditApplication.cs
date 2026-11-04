using System.ComponentModel.DataAnnotations;

namespace SalesInvoiceService.API.Models
{
    public class CustomerCreditApplication
    {
        public int Id { get; set; }

        public Guid RequestId { get; set; }

        public int SourceInvoiceId { get; set; }

        public SalesInvoice SourceInvoice { get; set; } = null!;

        public int TargetInvoiceId { get; set; }

        public SalesInvoice TargetInvoice { get; set; } = null!;

        public Guid ClientId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Branch { get; set; } = string.Empty;

        [Required]
        [MaxLength(3)]
        public string Currency { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public DateTime AppliedAtUtc { get; set; }
    }
}
