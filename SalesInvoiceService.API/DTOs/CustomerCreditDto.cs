namespace SalesInvoiceService.API.DTOs
{
    public class CustomerCreditDto
    {
        public Guid ClientId { get; set; }

        public string ClientName { get; set; } = string.Empty;

        public string Branch { get; set; } = string.Empty;

        public string Currency { get; set; } = string.Empty;

        public decimal TotalAvailable { get; set; }

        public List<CustomerCreditSourceDto> Sources { get; set; }
            = new();

        public List<CustomerCreditApplicationDto> Applications { get; set; }
            = new();

        public List<CustomerCreditTargetDto> Targets { get; set; }
            = new();
    }

    public class CustomerCreditSourceDto
    {
        public int SalesInvoiceId { get; set; }

        public string InvoiceNumber { get; set; } = string.Empty;

        public decimal PaidAmount { get; set; }

        public decimal CreditedAmount { get; set; }

        public decimal AppliedFromOtherInvoices { get; set; }

        public decimal AppliedToOtherInvoices { get; set; }

        public decimal AvailableAmount { get; set; }

        public List<string> CreditNoteNumbers { get; set; }
            = new();
    }

    public class CustomerCreditTargetDto
    {
        public int SalesInvoiceId { get; set; }

        public string InvoiceNumber { get; set; } = string.Empty;

        public decimal BalanceDue { get; set; }
    }
}
