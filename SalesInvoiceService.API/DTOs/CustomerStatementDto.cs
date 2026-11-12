namespace SalesInvoiceService.API.DTOs
{
    public class CustomerStatementDto
    {
        public Guid ClientId { get; set; }

        public string ClientName { get; set; } = string.Empty;

        public string Branch { get; set; } = string.Empty;

        public string Currency { get; set; } = string.Empty;

        public DateOnly? FromDate { get; set; }

        public DateOnly? ToDate { get; set; }

        public decimal OpeningBalance { get; set; }

        public decimal InvoiceTotal { get; set; }

        public decimal CreditNoteTotal { get; set; }

        public decimal CashPaymentTotal { get; set; }

        public decimal NetBalance { get; set; }

        public List<CustomerStatementLineDto> Lines { get; set; } = [];
    }

    public class CustomerStatementLineDto
    {
        public DateOnly Date { get; set; }

        public string Type { get; set; } = string.Empty;

        public string DocumentNumber { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string InvoiceNumber { get; set; } = string.Empty;

        public string? TargetInvoiceNumber { get; set; }

        public decimal Amount { get; set; }

        public decimal BalanceChange { get; set; }

        public decimal RunningBalance { get; set; }
    }

    public class CustomerStatementClientDto
    {
        public Guid ClientId { get; set; }

        public string ClientName { get; set; } = string.Empty;

        public string Branch { get; set; } = string.Empty;

        public string Currency { get; set; } = string.Empty;
    }
}
