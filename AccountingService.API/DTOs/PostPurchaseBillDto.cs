using System.ComponentModel.DataAnnotations;

namespace AccountingService.API.DTOs
{
    public class PostPurchaseBillDto
    {
        [Range(1, int.MaxValue)] public int CompanyId { get; set; }
        [Range(1, int.MaxValue)] public int BillId { get; set; }
        [Required, MaxLength(100)] public string BillNumber { get; set; } = string.Empty;
        public DateOnly BillDate { get; set; }
        [Required, MaxLength(100)] public string Branch { get; set; } = string.Empty;
        [Required, StringLength(3, MinimumLength = 3)] public string Currency { get; set; } = string.Empty;
        [Range(typeof(decimal), "0.00000001", "9999999999.99999999")]
        public decimal ExchangeRate { get; set; } = 1m;
        public Guid VendorId { get; set; }
        [Required, MaxLength(200)] public string VendorName { get; set; } = string.Empty;
        [Range(typeof(decimal), "0.0001", "999999999999999.9999")]
        public decimal NetAmount { get; set; }
        [Range(typeof(decimal), "0.0001", "999999999999999.9999")]
        public decimal TotalAmount { get; set; }
        [Required, MinLength(1)]
        public List<PurchaseBillExpensePostingDto> Expenses { get; set; } = new();
        public List<PurchaseBillTaxPostingDto> Taxes { get; set; } = new();
    }

    public class PurchaseBillExpensePostingDto
    {
        [Required, MaxLength(30)] public string AccountCode { get; set; } = string.Empty;
        [Required, MaxLength(200)] public string Description { get; set; } = string.Empty;
        [Range(typeof(decimal), "0.0001", "999999999999999.9999")]
        public decimal Amount { get; set; }
    }

    public class PurchaseBillTaxPostingDto
    {
        [Required, MaxLength(60)] public string SystemKey { get; set; } = string.Empty;
        [Required, MaxLength(200)] public string Description { get; set; } = string.Empty;
        [Range(typeof(decimal), "0.0001", "999999999999999.9999")]
        public decimal Amount { get; set; }
    }

    public class PurchaseBillPostingResponseDto
    {
        public int JournalEntryId { get; set; }
        public string EntryNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool AlreadyExisted { get; set; }
    }
}
