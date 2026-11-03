namespace SalesInvoiceService.API.DTOs
{
    public class AccountingCompanyDto
    {
        public int Id { get; set; }

        public string Name { get; set; }
            = string.Empty;

        public string Country { get; set; }
            = string.Empty;

        public string Currency { get; set; }
            = string.Empty;
    }

    public class SalesInvoiceAccountingPostingDto
    {
        public int CompanyId { get; set; }

        public int InvoiceId { get; set; }

        public string InvoiceNumber { get; set; }
            = string.Empty;

        public DateOnly InvoiceDate { get; set; }

        public string Branch { get; set; }
            = string.Empty;

        public string Currency { get; set; }
            = string.Empty;

        public decimal ExchangeRate { get; set; }
            = 1m;

        public Guid CustomerId { get; set; }

        public string CustomerName { get; set; }
            = string.Empty;

        public decimal NetAmount { get; set; }

        public decimal TotalAmount { get; set; }

        public List<SalesInvoiceAccountingTaxDto> Taxes { get; set; }
            = new();
    }

    public class SalesInvoiceAccountingTaxDto
    {
        public string SystemKey { get; set; }
            = string.Empty;

        public string Description { get; set; }
            = string.Empty;

        public decimal Amount { get; set; }
    }

    public class SalesInvoiceAccountingResultDto
    {
        public int JournalEntryId { get; set; }

        public string EntryNumber { get; set; }
            = string.Empty;

        public string Status { get; set; }
            = string.Empty;

        public bool AlreadyExisted { get; set; }
    }
}
