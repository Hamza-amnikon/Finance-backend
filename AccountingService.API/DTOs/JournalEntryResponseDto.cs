namespace AccountingService.API.DTOs
{
    public class JournalEntryResponseDto
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }

        public string CompanyName { get; set; }
            = string.Empty;

        public string EntryNumber { get; set; }
            = string.Empty;

        public DateOnly EntryDate { get; set; }

        public string Branch { get; set; }
            = string.Empty;

        public string Currency { get; set; }
            = string.Empty;

        public decimal ExchangeRate { get; set; }

        public string Narration { get; set; }
            = string.Empty;

        public string? Reference { get; set; }

        public string Source { get; set; }
            = string.Empty;

        public string? SourceDocumentId { get; set; }

        public string? SourceDocumentNumber { get; set; }

        public string Status { get; set; }
            = string.Empty;

        public decimal TotalDebit { get; set; }

        public decimal TotalCredit { get; set; }

        public int? ReversalOfJournalEntryId { get; set; }

        public int? ReversedByJournalEntryId { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? ModifiedDate { get; set; }

        public DateTime? PostedDate { get; set; }

        public DateTime? ReversedDate { get; set; }

        public string RowVersion { get; set; }
            = string.Empty;

        public List<JournalEntryLineResponseDto> Lines { get; set; }
            = new();
    }

    public class JournalEntryLineResponseDto
    {
        public int Id { get; set; }

        public int LineNumber { get; set; }

        public int AccountId { get; set; }

        public string AccountCode { get; set; }
            = string.Empty;

        public string AccountName { get; set; }
            = string.Empty;

        public string Description { get; set; }
            = string.Empty;

        public decimal DebitAmount { get; set; }

        public decimal CreditAmount { get; set; }

        public decimal BaseDebitAmount { get; set; }

        public decimal BaseCreditAmount { get; set; }

        public string? CostCenter { get; set; }

        public string? CounterpartyType { get; set; }

        public string? CounterpartyId { get; set; }
    }
}