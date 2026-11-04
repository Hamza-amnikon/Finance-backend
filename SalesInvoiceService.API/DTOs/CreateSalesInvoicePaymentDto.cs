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

        [Required]
        [MaxLength(200)]
        public string Reference { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string Notes { get; set; } = string.Empty;

        [Range(1, int.MaxValue)]
        public int? BankAccountId { get; set; }

        [RegularExpression("^[A-Za-z]{3}$")]
        public string? BankCurrency { get; set; }

        [Range(
            typeof(decimal),
            "0.0001",
            "99999999999999.9999")]
        public decimal? BankAmount { get; set; }

        [Range(
            typeof(decimal),
            "0",
            "99999999999999.9999")]
        public decimal BankChargeAmount { get; set; }

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

            if (string.IsNullOrWhiteSpace(Reference))
            {
                yield return new ValidationResult(
                    "Enter a payment reference.",
                    new[] { nameof(Reference) });
            }

            var hasBankSettlement =
                BankAccountId.HasValue ||
                BankAmount.HasValue ||
                !string.IsNullOrWhiteSpace(BankCurrency) ||
                BankChargeAmount > 0;

            if (hasBankSettlement &&
                !BankAccountId.HasValue)
            {
                yield return new ValidationResult(
                    "Select the receiving bank account.",
                    new[] { nameof(BankAccountId) });
            }

            if (hasBankSettlement &&
                !BankAmount.HasValue)
            {
                yield return new ValidationResult(
                    "Enter the actual amount credited by the bank.",
                    new[] { nameof(BankAmount) });
            }

            if (hasBankSettlement &&
                string.IsNullOrWhiteSpace(BankCurrency))
            {
                yield return new ValidationResult(
                    "The receiving bank currency is required.",
                    new[] { nameof(BankCurrency) });
            }
        }
    }
}
