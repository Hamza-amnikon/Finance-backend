using BankingService.API.Models;

namespace BankingService.API.DTOs
{
    public class BankTransactionResponseDto
    {
        public int Id { get; set; }

        public Guid RequestId { get; set; }

        public int BankAccountId { get; set; }

        public string BankAccountName { get; set; } = "";

        public int CompanyId { get; set; }

        public string CompanyName { get; set; } = "";

        public DateOnly TransactionDate { get; set; }

        public BankTransactionDirection Direction { get; set; }

        public decimal Amount { get; set; }

        public string Currency { get; set; } = "";

        public int CurrencyDecimalPlaces { get; set; }

        public string Status { get; set; } = "";

        public string Reference { get; set; } = "";

        public string Description { get; set; } = "";

        public string Notes { get; set; } = "";

        public int? ReversalOfTransactionId { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? ModifiedDate { get; set; }

        public DateTime? PostedDate { get; set; }

        public DateTime? CancelledDate { get; set; }

        public byte[] RowVersion { get; set; } = [];
    }
}