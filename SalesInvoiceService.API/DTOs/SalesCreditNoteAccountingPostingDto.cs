namespace SalesInvoiceService.API.DTOs
{
    public class SalesCreditNoteAccountingPostingDto
    {
        public int CompanyId { get; set; }

        public int CreditNoteId { get; set; }

        public string CreditNoteNumber { get; set; }
            = string.Empty;

        public DateOnly CreditNoteDate { get; set; }

        public int InvoiceId { get; set; }

        public string InvoiceNumber { get; set; }
            = string.Empty;

        public string Branch { get; set; }
            = string.Empty;

        public string Currency { get; set; }
            = string.Empty;

        public Guid CustomerId { get; set; }

        public string CustomerName { get; set; }
            = string.Empty;

        public decimal NetAmount { get; set; }

        public decimal TotalAmount { get; set; }

        public List<SalesCreditNoteAccountingTaxDto> Taxes { get; set; }
            = new();
    }

    public class SalesCreditNoteAccountingTaxDto
    {
        public string SystemKey { get; set; }
            = string.Empty;

        public string Description { get; set; }
            = string.Empty;

        public decimal Amount { get; set; }
    }

    public class SalesCreditNoteAccountingResultDto
    {
        public int JournalEntryId { get; set; }

        public string EntryNumber { get; set; }
            = string.Empty;

        public string Status { get; set; }
            = string.Empty;

        public bool AlreadyExisted { get; set; }
    }
}
