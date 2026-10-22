using System.ComponentModel.DataAnnotations;

namespace BankingService.API.DTOs
{
    public class CreateBankAccountDto : IValidatableObject
    {
        [Range(1, int.MaxValue)]
        public int CompanyId { get; set; }

        [Required]
        [MaxLength(160)]
        public string Name { get; set; } = "";

        [Required]
        [MaxLength(160)]
        public string BankName { get; set; } = "";

        [Required]
        [MaxLength(50)]
        public string AccountNumber { get; set; } = "";

        [Required]
        [RegularExpression("^[A-Za-z]{3}$")]
        public string Currency { get; set; } = "";

        public decimal OpeningBalance { get; set; }

        public DateOnly OpeningBalanceDate { get; set; }

        public IEnumerable<ValidationResult> Validate(
            ValidationContext context)
        {
            if (OpeningBalanceDate == default)
            {
                yield return new ValidationResult(
                    "Opening balance date is required.",
                    new[] { nameof(OpeningBalanceDate) });
            }
        }
    }
}