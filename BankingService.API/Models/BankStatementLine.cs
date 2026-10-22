using System.ComponentModel.DataAnnotations;

namespace BankingService.API.Models
{
    public class BankStatementLine
    {
        public int Id { get; set; }

        public int BankReconciliationId { get; set; }

        public BankReconciliation BankReconciliation { get; set; } = null!;

        public int LineNumber { get; set; }

        public DateOnly TransactionDate { get; set; }

        public BankTransactionDirection Direction { get; set; }

        public decimal Amount { get; set; }

        [MaxLength(200)]
        public string Reference { get; set; } = "";

        [MaxLength(1000)]
        public string Description { get; set; } = "";

        public int? MatchedBankTransactionId { get; set; }

        public BankTransaction? MatchedBankTransaction { get; set; }

        public DateTime? MatchedDate { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
    }
}