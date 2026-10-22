using System.ComponentModel.DataAnnotations;

namespace BankingService.API.DTOs
{
    public class ConfirmBankTransferSentDto : IValidatableObject
    {
        [Range(
            typeof(decimal),
            "0.0001",
            "99999999999999.9999")]
        public decimal SourceAmount { get; set; }

        [Range(
            typeof(decimal),
            "0",
            "99999999999999.9999")]
        public decimal SourceFeeAmount { get; set; }

        public DateOnly TransferDate { get; set; }

        [MaxLength(200)]
        public string Reference { get; set; } = "";

        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] RowVersion { get; set; } = [];

        public IEnumerable<ValidationResult> Validate(
            ValidationContext context)
        {
            if (TransferDate == default)
            {
                yield return new ValidationResult(
                    "Actual debit date is required.",
                    new[] { nameof(TransferDate) });
            }
        }
    }
}