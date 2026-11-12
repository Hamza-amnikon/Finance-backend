namespace AccountingService.API.DTOs
{
    public class JournalEntryListDto
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

        public string Narration { get; set; }
            = string.Empty;

        public string? Reference { get; set; }

        public string Source { get; set; }
            = string.Empty;

        public string? SourceDocumentNumber { get; set; }

        public string Status { get; set; }
            = string.Empty;

        public decimal TotalDebit { get; set; }

        public decimal TotalCredit { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? PostedDate { get; set; }
    }

    public class JournalEntryPagedResultDto
    {
        public IReadOnlyList<JournalEntryListDto> Data { get; set; } = [];

        public int Total { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }
}