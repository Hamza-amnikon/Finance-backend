using System.ComponentModel.DataAnnotations;

namespace ExpenseService.API.DTOs
{
    public class ExpenseClaimDto
    {
        public Guid RequestId { get; set; }
        public string RowVersion { get; set; } = "";

        [Range(1, int.MaxValue)]
        public int CompanyId { get; set; }

        [Required, MaxLength(100)]
        public string Branch { get; set; } = "";

        [Required, StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; } = "";
        public DateOnly Date { get; set; }

        [Required, MaxLength(100)]
        public string Reference { get; set; } = "";

        [Required, MaxLength(200)]
        public string Employee { get; set; } = "";

        [Required, MaxLength(80)]
        public string Category { get; set; } = "";

        [Required, MaxLength(1000)]
        public string Purpose { get; set; } = "";

        [MaxLength(2000)]
        public string Notes { get; set; } = "";

        [Range(typeof(decimal), "0.01", "999999999999.99")]
        public decimal Amount { get; set; }
    }

    public class ExpenseClaimActionDto
    {
        [Required]
        public string RowVersion { get; set; } = "";

        [Required, MaxLength(20)]
        public string Status { get; set; } = "";

        [MaxLength(1000)]
        public string Reason { get; set; } = "";
        public DateOnly? ApprovedDate { get; set; }
        public DateOnly? PaidDate { get; set; }

        [MaxLength(200)]
        public string PaymentReference { get; set; } = "";
        public int? ExpenseAccountId { get; set; }
        public int? PayableAccountId { get; set; }
        public int? PaymentAccountId { get; set; }
    }
}
