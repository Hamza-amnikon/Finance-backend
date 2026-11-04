using System.ComponentModel.DataAnnotations;

namespace AccountingService.API.DTOs
{
    public class PostSalesCreditNoteDto
    {
        [Range(1, int.MaxValue)]
        public int CompanyId { get; set; }

        [Range(1, int.MaxValue)]
        public int CreditNoteId { get; set; }

        [Required]
        [MaxLength(100)]
        public string CreditNoteNumber { get; set; }
            = string.Empty;

        public DateOnly CreditNoteDate { get; set; }

        [Range(1, int.MaxValue)]
        public int InvoiceId { get; set; }

        [Required]
        [MaxLength(100)]
        public string InvoiceNumber { get; set; }
            = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Branch { get; set; }
            = string.Empty;

        [Required]
        [StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; }
            = string.Empty;

        public Guid CustomerId { get; set; }

        [Required]
        [MaxLength(200)]
        public string CustomerName { get; set; }
            = string.Empty;

        [Range(
            typeof(decimal),
            "0.0001",
            "999999999999999.9999")]
        public decimal NetAmount { get; set; }

        [Range(
            typeof(decimal),
            "0.0001",
            "999999999999999.9999")]
        public decimal TotalAmount { get; set; }

        public List<SalesCreditNoteTaxPostingDto> Taxes { get; set; }
            = new();
    }

    public class SalesCreditNoteTaxPostingDto
    {
        [Required]
        [MaxLength(60)]
        public string SystemKey { get; set; }
            = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Description { get; set; }
            = string.Empty;

        [Range(
            typeof(decimal),
            "0.0001",
            "999999999999999.9999")]
        public decimal Amount { get; set; }
    }

    public class SalesCreditNotePostingResponseDto
    {
        public int JournalEntryId { get; set; }

        public string EntryNumber { get; set; }
            = string.Empty;

        public string Status { get; set; }
            = string.Empty;

        public bool AlreadyExisted { get; set; }
    }
}
