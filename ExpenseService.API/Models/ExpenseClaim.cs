using System.ComponentModel.DataAnnotations;

namespace ExpenseService.API.Models
{
    public class ExpenseClaim
    {
        public int Id { get; set; }
        public Guid RequestId { get; set; }
        public int CompanyId { get; set; }
        [MaxLength(100)] public string Branch { get; set; } = "";
        [MaxLength(3)] public string Currency { get; set; } = "";
        public DateOnly Date { get; set; }
        [MaxLength(100)] public string Reference { get; set; } = "";
        [MaxLength(200)] public string Employee { get; set; } = "";
        [MaxLength(80)] public string Category { get; set; } = "";
        [MaxLength(1000)] public string Purpose { get; set; } = "";
        [MaxLength(2000)] public string Notes { get; set; } = "";
        public decimal Amount { get; set; }
        public decimal ExchangeRate { get; set; }
        public DateOnly? RateDate { get; set; }
        [MaxLength(3)] public string ReportingCurrency { get; set; } = "";
        [MaxLength(20)] public string Status { get; set; } = "Draft";
        [MaxLength(200)] public string CreatedBy { get; set; } = "";
        [MaxLength(200)]
        public string? ApprovedBy { get; set; }
        [MaxLength(200)]
        public string? PaidBy { get; set; }
        [MaxLength(1000)] public string DecisionReason { get; set; } = "";
        public DateOnly? ApprovedDate { get; set; }
        public DateOnly? PaidDate { get; set; }
        [MaxLength(200)] public string PaymentReference { get; set; } = "";
        public int? ExpenseAccountId { get; set; }
        public int? PayableAccountId { get; set; }
        public int? PaymentAccountId { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;
        [Timestamp] public byte[] RowVersion { get; set; } = [];
        public ICollection<ExpenseClaimReceipt> Receipts { get; set; } = new List<ExpenseClaimReceipt>();
        public ICollection<ExpenseClaimHistory> History { get; set; } = new List<ExpenseClaimHistory>();
    }

    public class ExpenseClaimReceipt
    {
        public int Id { get; set; }
        public int ExpenseClaimId { get; set; }
        [MaxLength(200)] public string FileName { get; set; } = "";
        [MaxLength(80)] public string ContentType { get; set; } = "";
        [System.Text.Json.Serialization.JsonIgnore] public byte[] Content { get; set; } = [];
        public long Size { get; set; }
    }

    public class ExpenseClaimHistory
    {
        public int Id { get; set; }
        public int ExpenseClaimId { get; set; }
        [MaxLength(200)] public string Actor { get; set; } = "";
        [MaxLength(20)] public string Status { get; set; } = "";
        [MaxLength(1000)] public string Reason { get; set; } = "";
        public DateTime AtUtc { get; set; } = DateTime.UtcNow;
    }
}
