using System.ComponentModel.DataAnnotations;

namespace AccountingService.API.Models
{
    public enum JournalEntryStatus
    {
        Draft,
        Posted,
        Reversed
    }

    public enum JournalEntrySource
    {
        Manual,
        OpeningBalance,
        SalesInvoice,
        PaymentReceived,
        PurchaseBill,
        PaymentMade,
        Expense,
        BankTransaction,
        Reversal
    }

    public class JournalEntry
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }

        public AccountingCompanyLookup Company { get; set; }
            = null!;

        [Required]
        [MaxLength(30)]
        public string EntryNumber { get; set; }
            = string.Empty;

        public DateOnly EntryDate { get; set; }

        [Required]
        [MaxLength(100)]
        public string Branch { get; set; }
            = string.Empty;

        [Required]
        [MaxLength(3)]
        public string Currency { get; set; }
            = string.Empty;

        public decimal ExchangeRate { get; set; } = 1m;

        [Required]
        [MaxLength(500)]
        public string Narration { get; set; }
            = string.Empty;

        [MaxLength(100)]
        public string? Reference { get; set; }

        public JournalEntrySource Source { get; set; }

        [MaxLength(100)]
        public string? SourceDocumentId { get; set; }

        [MaxLength(100)]
        public string? SourceDocumentNumber { get; set; }

        public JournalEntryStatus Status { get; set; }
            = JournalEntryStatus.Draft;

        public decimal TotalDebit { get; set; }

        public decimal TotalCredit { get; set; }

        public int? ReversalOfJournalEntryId { get; set; }

        public JournalEntry? ReversalOfJournalEntry { get; set; }

        public ICollection<JournalEntry> ReversalEntries { get; set; }
            = new List<JournalEntry>();

        public ICollection<JournalEntryLine> Lines { get; set; }
            = new List<JournalEntryLine>();

        public DateTime CreatedDate { get; set; }
            = DateTime.UtcNow;

        public DateTime? ModifiedDate { get; set; }

        public DateTime? PostedDate { get; set; }

        public DateTime? ReversedDate { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
    }
}