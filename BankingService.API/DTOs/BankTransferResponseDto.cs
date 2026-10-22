using BankingService.API.Models;

namespace BankingService.API.DTOs
{
    public class BankTransferResponseDto
    {
        public int Id { get; set; }

        public Guid RequestId { get; set; }

        public int SourceBankAccountId { get; set; }

        public string SourceBankAccountName { get; set; } = "";

        public int SourceCompanyId { get; set; }

        public string SourceCompanyName { get; set; } = "";

        public int DestinationBankAccountId { get; set; }

        public string DestinationBankAccountName { get; set; } = "";

        public int DestinationCompanyId { get; set; }

        public string DestinationCompanyName { get; set; } = "";

        public string SourceCurrency { get; set; } = "";

        public int SourceCurrencyDecimalPlaces { get; set; }

        public string DestinationCurrency { get; set; } = "";

        public int DestinationCurrencyDecimalPlaces { get; set; }

        public decimal SourceAmount { get; set; }

        public decimal SourceFeeAmount { get; set; }

        public decimal? DestinationAmount { get; set; }

        public decimal? EffectiveExchangeRate { get; set; }

        public DateOnly TransferDate { get; set; }

        public DateOnly? ReceivedDate { get; set; }

        public BankTransferStatus Status { get; set; }

        public string Reference { get; set; } = "";

        public string Notes { get; set; } = "";

        public int? SourceTransactionId { get; set; }

        public int? SourceFeeTransactionId { get; set; }

        public int? DestinationTransactionId { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? ModifiedDate { get; set; }

        public DateTime? SentDate { get; set; }

        public DateTime? CompletedDate { get; set; }

        public DateTime? CancelledDate { get; set; }

        public byte[] RowVersion { get; set; } = [];
    }
}