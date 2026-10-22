using System.ComponentModel.DataAnnotations;

namespace BankingService.API.DTOs
{
    public class CreateBankTransferDto : IValidatableObject
    {
        public Guid RequestId { get; set; }

        [Range(1, int.MaxValue)]
        public int SourceBankAccountId { get; set; }

        [Range(1, int.MaxValue)]
        public int DestinationBankAccountId { get; set; }

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

            if (SourceBankAccountId == DestinationBankAccountId)
            {
                yield return new ValidationResult(
                    "Source and destination accounts must be different.",
                    new[]
                    {
                        nameof(SourceBankAccountId),
                        nameof(DestinationBankAccountId)
                    });
            }

            if (TransferDate == default)
            {
                yield return new ValidationResult(
                    "Transfer date is required.",
                    new[] { nameof(TransferDate) });
            }
        }
    }
}