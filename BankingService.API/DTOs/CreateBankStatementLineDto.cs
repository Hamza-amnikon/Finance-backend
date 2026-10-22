using System.ComponentModel.DataAnnotations;
using BankingService.API.Models;

namespace BankingService.API.DTOs
{
    public class CreateBankStatementLineDto : IValidatableObject
    {
        public DateOnly TransactionDate { get; set; }

        [EnumDataType(typeof(BankTransactionDirection))]
        public BankTransactionDirection Direction { get; set; }

        [Range(
            typeof(decimal),
            "0.0001",
            "99999999999999.9999")]
        public decimal Amount { get; set; }

        [MaxLength(200)]
        public string Reference { get; set; } = "";

        [MaxLength(1000)]
        public string Description { get; set; } = "";

        public IEnumerable<ValidationResult> Validate(
            ValidationContext context)
        {
            if (TransactionDate == default)
            {
                yield return new ValidationResult(
                    "Statement transaction date is required.",
                    new[] { nameof(TransactionDate) });
            }
        }
    }
}