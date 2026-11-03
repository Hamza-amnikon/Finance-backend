namespace AccountingService.API.DTOs
{
    public class GeneralLedgerLineDto
    {
        public int JournalEntryId { get; set; }

        public int JournalEntryLineId { get; set; }

        public int LineNumber { get; set; }

        public string EntryNumber { get; set; }
            = string.Empty;

        public DateOnly EntryDate { get; set; }

        public string Branch { get; set; }
            = string.Empty;

        public string Source { get; set; }
            = string.Empty;

        public string Status { get; set; }
            = string.Empty;

        public string? SourceDocumentNumber { get; set; }

        public string? Reference { get; set; }

        public string Narration { get; set; }
            = string.Empty;

        public string Description { get; set; }
            = string.Empty;

        public string TransactionCurrency { get; set; }
            = string.Empty;

        public decimal ExchangeRate { get; set; }

        public decimal TransactionDebit { get; set; }

        public decimal TransactionCredit { get; set; }

        public decimal BaseDebit { get; set; }

        public decimal BaseCredit { get; set; }

        public decimal RunningBalance { get; set; }

        public string BalanceSide { get; set; }
            = string.Empty;

        public string? CostCenter { get; set; }

        public string? CounterpartyType { get; set; }

        public string? CounterpartyId { get; set; }
    }
}