using System.ComponentModel.DataAnnotations;

namespace BankingService.API.Models
{
    public class BankReconciliation
    {
        public int Id { get; set; }

        public Guid RequestId { get; set; }

        [MaxLength(64)]
        public string RequestFingerprint { get; set; } = "";

        public int BankAccountId { get; set; }

        public BankAccount BankAccount { get; set; } = null!;

        public int CompanyId { get; set; }

        [Required]
        [MaxLength(3)]
        public string Currency { get; set; } = "";

        public int CurrencyDecimalPlaces { get; set; }

        public DateOnly PeriodStart { get; set; }

        public DateOnly PeriodEnd { get; set; }

        public decimal StatementOpeningBalance { get; set; }

        public decimal StatementClosingBalance { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Draft";

        [MaxLength(2000)]
        public string Notes { get; set; } = "";

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ModifiedDate { get; set; }

        public DateTime? CompletedDate { get; set; }

        public List<BankStatementLine> Lines { get; set; } = [];

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
    }
}