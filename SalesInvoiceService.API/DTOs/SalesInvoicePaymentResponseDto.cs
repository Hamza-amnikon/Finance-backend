namespace SalesInvoiceService.API.DTOs
{
    public class SalesInvoicePaymentResponseDto
    {
        public int Id { get; set; }

        public int SalesInvoiceId { get; set; }

        public Guid RequestId { get; set; }

        public string PaymentNumber { get; set; } = string.Empty;

        public string InvoiceNumber { get; set; } = string.Empty;

        public Guid ClientId { get; set; }

        public string ClientName { get; set; } = string.Empty;

        public string Branch { get; set; } = string.Empty;

        public DateOnly PaymentDate { get; set; }

        public decimal Amount { get; set; }

        public string Currency { get; set; } = string.Empty;

        public string PaymentMode { get; set; } = string.Empty;

        public string Reference { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;

        public int? BankAccountId { get; set; }

        public string BankAccountName { get; set; } = string.Empty;

        public string BankCurrency { get; set; } = string.Empty;

        public decimal? BankAmount { get; set; }

        public decimal BankChargeAmount { get; set; }

        public decimal? SettlementExchangeRate { get; set; }

        public DateTime CreatedDate { get; set; }
    }
}
