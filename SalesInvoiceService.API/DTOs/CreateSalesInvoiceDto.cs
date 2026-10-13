using System.ComponentModel.DataAnnotations;

namespace SalesInvoiceService.API.DTOs
{
    public class CreateSalesInvoiceDto : IValidatableObject
    {
        public Guid ClientId { get; set; }

        public DateOnly InvoiceDate { get; set; }

        public DateOnly DueDate { get; set; }

        public DateOnly? ServicePeriodStart { get; set; }

        public DateOnly? ServicePeriodEnd { get; set; }

        [Required]
        [RegularExpression("^[A-Za-z]{3}$")]
        public string Currency { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Branch { get; set; } = string.Empty;

        [MaxLength(200)]
        public string PaymentTerms { get; set; } = string.Empty;

        [MaxLength(50)]
        public string PreferredPaymentMode { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Reference { get; set; } = string.Empty;

        // Optional override; otherwise use the client's address.
        [MaxLength(1000)]
        public string? BillingAddress { get; set; }

        [MaxLength(2000)]
        public string Notes { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string AdditionalTerms { get; set; } = string.Empty;

        [Required]
        [MinLength(1)]
        public List<SalesInvoiceItemRequestDto> Items { get; set; } =
            new List<SalesInvoiceItemRequestDto>();

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (ClientId == Guid.Empty)
            {
                yield return new ValidationResult(
                    "Select a client.",
                    new[] { nameof(ClientId) });
            }

            if (InvoiceDate == default)
            {
                yield return new ValidationResult(
                    "Invoice date is required.",
                    new[] { nameof(InvoiceDate) });
            }

            if (DueDate == default)
            {
                yield return new ValidationResult(
                    "Due date is required.",
                    new[] { nameof(DueDate) });
            }
            else if (InvoiceDate != default && DueDate < InvoiceDate)
            {
                yield return new ValidationResult(
                    "Due date cannot be before invoice date.",
                    new[] { nameof(DueDate) });
            }

            if (ServicePeriodStart.HasValue != ServicePeriodEnd.HasValue)
            {
                yield return new ValidationResult(
                    "Provide both service period dates, or leave both empty.",
                    new[]
                    {
                        nameof(ServicePeriodStart),
                        nameof(ServicePeriodEnd)
                    });
            }
            else if (
                ServicePeriodStart.HasValue &&
                ServicePeriodEnd.HasValue &&
                ServicePeriodEnd.Value < ServicePeriodStart.Value)
            {
                yield return new ValidationResult(
                    "Service period end cannot be before its start.",
                    new[] { nameof(ServicePeriodEnd) });
            }
        }
    }
}