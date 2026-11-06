using System.ComponentModel.DataAnnotations;

namespace PaymentMadeService.API.DTOs
{
    public class CreatePaymentMadeDto : IValidatableObject
    {
        public Guid RequestId { get; set; }

        public Guid VendorId { get; set; }

        [Range(1, int.MaxValue)]
        public int BillId { get; set; }

        public DateOnly PaymentDate { get; set; }

        public decimal Amount { get; set; }

        [Range(1, int.MaxValue)]
        public int PaymentModeId { get; set; }

        [MaxLength(200)]
        public string Reference { get; set; } = "";

        [MaxLength(2000)]
        public string Notes { get; set; } = "";

        [Range(1, int.MaxValue)]
        public int? BankAccountId { get; set; }

        [RegularExpression("^[A-Za-z]{3}$")]
        public string? BankCurrency { get; set; }

        [Range(
            typeof(decimal),
            "0.0001",
            "99999999999999.9999")]
        public decimal? BankAmount { get; set; }

        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] BillRowVersion { get; set; } = [];

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            var hasBankSettlement =
                BankAccountId.HasValue ||
                BankAmount.HasValue ||
                !string.IsNullOrWhiteSpace(BankCurrency);

            if (hasBankSettlement && !BankAccountId.HasValue)
            {
                yield return new ValidationResult(
                    "Select the paying bank account.",
                    new[] { nameof(BankAccountId) });
            }

            if (hasBankSettlement && !BankAmount.HasValue)
            {
                yield return new ValidationResult(
                    "Enter the actual amount paid by the bank.",
                    new[] { nameof(BankAmount) });
            }

            if (hasBankSettlement &&
                string.IsNullOrWhiteSpace(BankCurrency))
            {
                yield return new ValidationResult(
                    "The paying bank currency is required.",
                    new[] { nameof(BankCurrency) });
            }
        }
    }
}
