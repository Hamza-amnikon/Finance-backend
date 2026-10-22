using System.ComponentModel.DataAnnotations;
using BankingService.API.Models;

namespace BankingService.API.DTOs
{
    public class CreateBankTransactionDto : IValidatableObject
    {
        public Guid RequestId { get; set; }

        [Range(1, int.MaxValue)]
        public int BankAccountId { get; set; }

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

        public IEnumerable<ValidationResult> Validate(
            ValidationContext context)
        {
            if (RequestId == Guid.Empty)
            {
                yield return new ValidationResult(
                    "Request ID is required.",
                    new[] { nameof(RequestId) });
            }

            if (TransactionDate == default)
            {
                yield return new ValidationResult(
                    "Transaction date is required.",
                    new[] { nameof(TransactionDate) });
            }
        }
    }
}