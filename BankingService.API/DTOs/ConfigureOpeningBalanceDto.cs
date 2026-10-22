using System.ComponentModel.DataAnnotations;

namespace BankingService.API.DTOs
{
    public class ConfigureOpeningBalanceDto : IValidatableObject
    {
        public decimal OpeningBalance { get; set; }

        public DateOnly OpeningBalanceDate { get; set; }

        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] RowVersion { get; set; } = [];

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