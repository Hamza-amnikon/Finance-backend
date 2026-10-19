using System.ComponentModel.DataAnnotations;

namespace SalesInvoiceService.API.DTOs
{
    public class CreateInvoiceFromEstimateDto : IValidatableObject
    {
        [Range(1, int.MaxValue)]
        public int EstimateId { get; set; }

        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] RowVersion { get; set; } = [];

        public DateOnly InvoiceDate { get; set; }

        public DateOnly DueDate { get; set; }

        [MaxLength(50)]
        public string PreferredPaymentMode { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (InvoiceDate == default)
                yield return new ValidationResult("Invoice date is required.", new[] { nameof(InvoiceDate) });

            if (DueDate == default || DueDate < InvoiceDate)
                yield return new ValidationResult("Due date must be on or after invoice date.", new[] { nameof(DueDate) });
        }
    }
}
