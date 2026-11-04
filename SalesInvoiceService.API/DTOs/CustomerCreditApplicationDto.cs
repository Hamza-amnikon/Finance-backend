namespace SalesInvoiceService.API.DTOs
{
    public class CustomerCreditApplicationDto
    {
        public int Id { get; set; }

        public Guid RequestId { get; set; }

        public int SourceInvoiceId { get; set; }

        public string SourceInvoiceNumber { get; set; }
            = string.Empty;

        public int TargetInvoiceId { get; set; }

        public string TargetInvoiceNumber { get; set; }
            = string.Empty;

        public Guid ClientId { get; set; }

        public string Branch { get; set; } = string.Empty;

        public string Currency { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public decimal SourceCreditRemaining { get; set; }

        public decimal TargetBalanceDue { get; set; }

        public DateTime AppliedAtUtc { get; set; }
    }
}
