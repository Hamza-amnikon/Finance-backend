using System.ComponentModel.DataAnnotations;

namespace BankingService.API.DTOs
{
    public class ConfirmBankTransferReceivedDto : IValidatableObject
    {
        [Range(
            typeof(decimal),
            "0.0001",
            "99999999999999.9999")]
        public decimal DestinationAmount { get; set; }

        public DateOnly ReceivedDate { get; set; }

        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] RowVersion { get; set; } = [];

        public IEnumerable<ValidationResult> Validate(
            ValidationContext context)
        {
            if (ReceivedDate == default)
            {
                yield return new ValidationResult(
                    "Actual receipt date is required.",
                    new[] { nameof(ReceivedDate) });
            }
        }
    }
}