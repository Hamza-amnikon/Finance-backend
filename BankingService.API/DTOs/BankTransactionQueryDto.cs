using System.ComponentModel.DataAnnotations;
using BankingService.API.Models;

namespace BankingService.API.DTOs
{
    public class BankTransactionQueryDto : IValidatableObject
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 25;

        [MaxLength(200)]
        public string? Search { get; set; }

        [Range(1, int.MaxValue)]
        public int? BankAccountId { get; set; }

        [Range(1, int.MaxValue)]
        public int? CompanyId { get; set; }

        [RegularExpression("^[A-Za-z]{3}$")]
        public string? Currency { get; set; }

        [EnumDataType(typeof(BankTransactionDirection))]
        public BankTransactionDirection? Direction { get; set; }

        [RegularExpression("^(Draft|Posted|Cancelled)$")]
        public string? Status { get; set; }

        public DateOnly? From { get; set; }

        public DateOnly? To { get; set; }

        public IEnumerable<ValidationResult> Validate(
            ValidationContext context)
        {
            if (
                From.HasValue &&
                To.HasValue &&
                From.Value > To.Value)
            {
                yield return new ValidationResult(
                    "From date cannot be later than To date.",
                    new[] { nameof(From), nameof(To) });
            }
        }
    }
}