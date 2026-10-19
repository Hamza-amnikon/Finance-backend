using System.ComponentModel.DataAnnotations;

namespace SalesEstimateService.API.DTOs
{
    public class ConvertSalesEstimateDto : SalesEstimateActionDto, IValidatableObject
    {
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
