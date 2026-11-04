using System.ComponentModel.DataAnnotations;

namespace AccountingService.API.DTOs
{
    public class PostPaymentReceivedDto
    {
        [Range(1, int.MaxValue)]
        public int CompanyId { get; set; }

        [Range(1, int.MaxValue)]
        public int PaymentId { get; set; }

        [Range(1, int.MaxValue)]
        public int? SalesInvoiceId { get; set; }

        [Required]
        [MaxLength(100)]
        public string PaymentNumber { get; set; }
            = string.Empty;

        public DateOnly PaymentDate { get; set; }

        [Required]
        [MaxLength(100)]
        public string Branch { get; set; }
            = string.Empty;

        [Required]
        [StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; }
            = string.Empty;

        [Range(
            typeof(decimal),
            "0.00000001",
            "9999999999.99999999")]
        public decimal ExchangeRate { get; set; }
            = 1m;

        [Required]
        [StringLength(3, MinimumLength = 3)]
        public string SettlementCurrency { get; set; }
            = string.Empty;

        [Range(
            typeof(decimal),
            "0.0001",
            "999999999999999.9999")]
        public decimal SettlementAmount { get; set; }

        [Range(
            typeof(decimal),
            "0",
            "999999999999999.9999")]
        public decimal BankChargeAmount { get; set; }

        public Guid CustomerId { get; set; }

        [Required]
        [MaxLength(200)]
        public string CustomerName { get; set; }
            = string.Empty;

        public int? BankAccountingAccountId { get; set; }

        public bool IsCash { get; set; }

        [Range(
            typeof(decimal),
            "0.0001",
            "999999999999999.9999")]
        public decimal Amount { get; set; }

        [MaxLength(100)]
        public string? Reference { get; set; }
    }

    public class PaymentReceivedPostingResponseDto
    {
        public int JournalEntryId { get; set; }

        public string EntryNumber { get; set; }
            = string.Empty;

        public string Status { get; set; }
            = string.Empty;

        public bool AlreadyExisted { get; set; }
    }
}
