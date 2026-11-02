using System.ComponentModel.DataAnnotations;

namespace AccountingService.API.Models
{
    public enum AccountType
    {
        Asset,
        Liability,
        Equity,
        Income,
        Expense
    }

    public enum AccountNormalBalance
    {
        Debit,
        Credit
    }

    public class Account
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }

        public AccountingCompanyLookup Company { get; set; } = null!;

        [Required]
        [MaxLength(30)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(160)]
        public string Name { get; set; } = string.Empty;

        public AccountType Type { get; set; }

        public AccountNormalBalance NormalBalance { get; set; }

        public int? ParentAccountId { get; set; }

        public Account? ParentAccount { get; set; }

        public ICollection<Account> ChildAccounts { get; set; }
            = new List<Account>();

        public bool IsPostingAccount { get; set; } = true;

        public bool IsControlAccount { get; set; }

        public bool IsSystemAccount { get; set; }

        [MaxLength(60)]
        public string? SystemKey { get; set; }

        public bool IsActive { get; set; } = true;

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ModifiedDate { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
    }
}