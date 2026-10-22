using System.ComponentModel.DataAnnotations;

namespace BankingService.API.DTOs
{
    public class CreateBankReconciliationDto : IValidatableObject
    {
        public Guid RequestId { get; set; }

        [Range(1, int.MaxValue)]
        public int BankAccountId { get; set; }

        public DateOnly PeriodStart { get; set; }

        public DateOnly PeriodEnd { get; set; }

        [Range(
            typeof(decimal),
            "-99999999999999.9999",
            "99999999999999.9999")]
        public decimal StatementOpeningBalance { get; set; }

        [Range(
            typeof(decimal),
            "-99999999999999.9999",
            "99999999999999.9999")]
        public decimal StatementClosingBalance { get; set; }

        [MaxLength(2000)]
        public string Notes { get; set; } = "";

        [Required]
        [MaxLength(2000)]
        public List<CreateBankStatementLineDto> Lines { get; set; } = [];

        public IEnumerable<ValidationResult> Validate(
            ValidationContext context)
        {
            if (RequestId == Guid.Empty)
            {
                yield return new ValidationResult(
                    "Request ID is required.",
                    new[] { nameof(RequestId) });
            }

            if (PeriodStart == default || PeriodEnd == default)
            {
                yield return new ValidationResult(
                    "Statement start and end dates are required.",
                    new[] { nameof(PeriodStart), nameof(PeriodEnd) });
            }

            if (PeriodStart > PeriodEnd)
            {
                yield return new ValidationResult(
                    "Statement start date cannot be after its end date.",
                    new[] { nameof(PeriodStart), nameof(PeriodEnd) });
            }
        }
    }
}