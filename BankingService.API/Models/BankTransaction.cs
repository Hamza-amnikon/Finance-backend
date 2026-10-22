using System.ComponentModel.DataAnnotations;

namespace BankingService.API.Models
{
    public class BankTransaction
    {
        public int Id { get; set; }

        public Guid RequestId { get; set; }

        [MaxLength(64)]
        public string RequestFingerprint { get; set; } = "";

        public int BankAccountId { get; set; }

        public BankAccount BankAccount { get; set; } = null!;

        public int CompanyId { get; set; }

        public DateOnly TransactionDate { get; set; }

        public BankTransactionDirection Direction { get; set; }

        public decimal Amount { get; set; }

        [Required]
        [MaxLength(3)]
        public string Currency { get; set; } = "";

        public int CurrencyDecimalPlaces { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Draft";

        [MaxLength(200)]
        public string Reference { get; set; } = "";

        [MaxLength(1000)]
        public string Description { get; set; } = "";

        [MaxLength(2000)]
        public string Notes { get; set; } = "";

        public int? ReversalOfTransactionId { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ModifiedDate { get; set; }

        public DateTime? PostedDate { get; set; }

        public DateTime? CancelledDate { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
    }
}
