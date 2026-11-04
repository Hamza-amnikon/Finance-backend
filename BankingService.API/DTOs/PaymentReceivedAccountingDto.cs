namespace BankingService.API.DTOs
{
    public class PaymentReceivedAccountingPostingDto
    {
        public int CompanyId { get; set; }

        public int PaymentId { get; set; }

        public int? SalesInvoiceId { get; set; }

        public string PaymentNumber { get; set; }
            = string.Empty;

        public DateOnly PaymentDate { get; set; }

        public string Branch { get; set; }
            = string.Empty;

        public string Currency { get; set; }
            = string.Empty;

        public decimal ExchangeRate { get; set; }

        public string SettlementCurrency { get; set; }
            = string.Empty;

        public decimal SettlementAmount { get; set; }

        public decimal BankChargeAmount { get; set; }

        public Guid CustomerId { get; set; }

        public string CustomerName { get; set; }
            = string.Empty;

        public int? BankAccountingAccountId { get; set; }

        public bool IsCash { get; set; }

        public decimal Amount { get; set; }

        public string? Reference { get; set; }
    }

    public class PaymentReceivedAccountingResultDto
    {
        public int JournalEntryId { get; set; }

        public string EntryNumber { get; set; }
            = string.Empty;

        public string Status { get; set; }
            = string.Empty;

        public bool AlreadyExisted { get; set; }
    }
}
