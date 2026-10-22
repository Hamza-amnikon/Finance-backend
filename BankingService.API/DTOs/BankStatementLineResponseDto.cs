using BankingService.API.Models;

namespace BankingService.API.DTOs
{
    public class BankStatementLineResponseDto
    {
        public int Id { get; set; }

        public int LineNumber { get; set; }

        public DateOnly TransactionDate { get; set; }

        public BankTransactionDirection Direction { get; set; }

        public decimal Amount { get; set; }

        public string Reference { get; set; } = "";

        public string Description { get; set; } = "";

        public int? MatchedBankTransactionId { get; set; }

        public DateTime? MatchedDate { get; set; }

        public byte[] RowVersion { get; set; } = [];
    }
}