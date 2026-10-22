using System.ComponentModel.DataAnnotations;
using BankingService.API.Models;

namespace BankingService.API.DTOs
{
    public class UpdateBankTransactionDto : IValidatableObject
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

        [MaxLength(2000)]
        public string Notes { get; set; } = "";

        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] RowVersion { get; set; } = [];

        public IEnumerable<ValidationResult> Validate(
            ValidationContext context)
        {
            if (TransactionDate == default)
            {
                yield return new ValidationResult(
                    "Transaction date is required.",
                    new[] { nameof(TransactionDate) });
            }
        }
    }
}