using System.ComponentModel.DataAnnotations;

namespace ExpenseService.API.DTOs
{
    public class ExpenseDto
    {
        public Guid RequestId { get; set; }

        [Range(1, int.MaxValue)]
        public int CompanyId { get; set; }

        public int? BankAccountId { get; set; }

        public DateOnly Date { get; set; }

        [Required]
        [MaxLength(160)]
        public string Supplier { get; set; } = "";

        [Required]
        [MaxLength(80)]
        public string Category { get; set; } = "";

        [MaxLength(1000)]
        public string Notes { get; set; } = "";

        [Range(typeof(decimal), "0.0001", "999999999999.9999")]
        public decimal Amount { get; set; }

        [Required]
        [RegularExpression("^[A-Z]{3}$")]
        public string Currency { get; set; } = "";

        public decimal Rate { get; set; }

        [Required]
        [RegularExpression("^[A-Z]{3}$")]
        public string RateBaseCurrency { get; set; } = "";

        [Required]
        [RegularExpression("^[A-Z]{3}$")]
        public string RateQuoteCurrency { get; set; } = "";

        public DateOnly RateDate { get; set; }

        [Required]
        [RegularExpression("^(Draft|Recorded)$")]
        public string Status { get; set; } = "Draft";

        [MaxLength(160)]
        public string Reference { get; set; } = "";

        public byte[]? RowVersion { get; set; }
    }

    public class VoidExpenseDto
    {
        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] RowVersion { get; set; } = [];

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = "";
    }
}
