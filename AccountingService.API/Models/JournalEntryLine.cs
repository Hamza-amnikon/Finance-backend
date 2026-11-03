using System.ComponentModel.DataAnnotations;

namespace AccountingService.API.Models
{
    public class JournalEntryLine
    {
        public int Id { get; set; }

        public int JournalEntryId { get; set; }

        public int CompanyId { get; set; }

        public JournalEntry JournalEntry { get; set; }
            = null!;

        public int LineNumber { get; set; }

        public int AccountId { get; set; }

        public Account Account { get; set; }
            = null!;

        [MaxLength(500)]
        public string Description { get; set; }
            = string.Empty;

        public decimal DebitAmount { get; set; }

        public decimal CreditAmount { get; set; }

        public decimal BaseDebitAmount { get; set; }

        public decimal BaseCreditAmount { get; set; }

        [MaxLength(100)]
        public string? CostCenter { get; set; }

        [MaxLength(50)]
        public string? CounterpartyType { get; set; }

        [MaxLength(100)]
        public string? CounterpartyId { get; set; }
    }
}