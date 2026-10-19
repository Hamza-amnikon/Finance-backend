using System.ComponentModel.DataAnnotations;

namespace SalesInvoiceService.API.DTOs
{
    public class CreateSalesInvoicePaymentDto : IValidatableObject
    {
        public Guid RequestId { get; set; }

        public DateOnly PaymentDate { get; set; }

        [Range(
            typeof(decimal),
            "0.0001",
            "99999999999999.9999",
            ErrorMessage = "Payment amount must be greater than zero and within the supported range.")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(50)]
        public string PaymentMode { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Reference { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string Notes { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (RequestId == Guid.Empty)
            {
                yield return new ValidationResult(
                    "A payment request ID is required.",
                    new[] { nameof(RequestId) });
            }

            if (PaymentDate == default)
            {
                yield return new ValidationResult(
                    "Payment date is required.",
                    new[] { nameof(PaymentDate) });
            }

            if (string.IsNullOrWhiteSpace(PaymentMode))
            {
                yield return new ValidationResult(
                    "Select a payment mode.",
                    new[] { nameof(PaymentMode) });
            }
        }
    }
}