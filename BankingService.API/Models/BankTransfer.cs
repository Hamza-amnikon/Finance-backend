using System.ComponentModel.DataAnnotations;

namespace BankingService.API.Models
{
    public class BankTransfer
    {
        public int Id { get; set; }

        public Guid RequestId { get; set; }

        [MaxLength(64)]
        public string RequestFingerprint { get; set; } = "";

        public int SourceBankAccountId { get; set; }

        public BankAccount SourceBankAccount { get; set; } = null!;

        public int SourceCompanyId { get; set; }

        public int DestinationBankAccountId { get; set; }

        public BankAccount DestinationBankAccount { get; set; } = null!;

        public int DestinationCompanyId { get; set; }

        [Required]
        [MaxLength(3)]
        public string SourceCurrency { get; set; } = "";

        public int SourceCurrencyDecimalPlaces { get; set; }

        [Required]
        [MaxLength(3)]
        public string DestinationCurrency { get; set; } = "";

        public int DestinationCurrencyDecimalPlaces { get; set; }

        public decimal SourceAmount { get; set; }

        public decimal SourceFeeAmount { get; set; }

        public decimal? DestinationAmount { get; set; }

        public decimal? EffectiveExchangeRate { get; set; }

        public DateOnly TransferDate { get; set; }

        public DateOnly? ReceivedDate { get; set; }

        public BankTransferStatus Status { get; set; } =
            BankTransferStatus.Draft;

        [MaxLength(200)]
        public string Reference { get; set; } = "";

        [MaxLength(2000)]
        public string Notes { get; set; } = "";

        public int? SourceTransactionId { get; set; }

        public int? SourceFeeTransactionId { get; set; }

        public int? DestinationTransactionId { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ModifiedDate { get; set; }

        public DateTime? SentDate { get; set; }

        public DateTime? CompletedDate { get; set; }

        public DateTime? CancelledDate { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
    }
}