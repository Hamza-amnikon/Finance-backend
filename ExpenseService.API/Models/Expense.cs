using System.ComponentModel.DataAnnotations;

namespace ExpenseService.API.Models
{
    public class Expense
    {
        public int Id { get; set; }

        public Guid RequestId { get; set; }

        public int CompanyId { get; set; }

        public Company Company { get; set; } = null!;

        public int? BankAccountId { get; set; }

        public BankAccount? BankAccount { get; set; }

        public DateOnly Date { get; set; }

        [MaxLength(160)]
        public string Supplier { get; set; } = "";

        [MaxLength(80)]
        public string Category { get; set; } = "";

        [MaxLength(1000)]
        public string Notes { get; set; } = "";

        public decimal Amount { get; set; }

        [MaxLength(3)]
        public string Currency { get; set; } = "";

        public int CurrencyDecimalPlaces { get; set; }

        // Saved rate: quote currency units for one base currency unit.
        public decimal Rate { get; set; }

        [MaxLength(3)]
        public string RateBaseCurrency { get; set; } = "";

        [MaxLength(3)]
        public string RateQuoteCurrency { get; set; } = "";

        public DateOnly RateDate { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "Draft";

        [MaxLength(160)]
        public string Reference { get; set; } = "";

        [MaxLength(500)]
        public string VoidReason { get; set; } = "";

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ModifiedDate { get; set; }

        public byte[] RowVersion { get; set; } = [];

        public Receipt? Receipt { get; set; }
    }
}
