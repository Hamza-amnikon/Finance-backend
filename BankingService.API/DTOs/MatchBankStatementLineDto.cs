using System.ComponentModel.DataAnnotations;

namespace BankingService.API.DTOs
{
    public class MatchBankStatementLineDto
    {
        // Null removes an existing draft match.
        [Range(1, int.MaxValue)]
        public int? BankTransactionId { get; set; }

        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] ReconciliationRowVersion { get; set; } = [];

        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] LineRowVersion { get; set; } = [];
    }
}