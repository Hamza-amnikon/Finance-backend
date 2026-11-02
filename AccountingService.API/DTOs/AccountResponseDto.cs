using AccountingService.API.Models;

namespace AccountingService.API.DTOs
{
    public class AccountResponseDto
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }

        public string CompanyName { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public string Currency { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public AccountType Type { get; set; }

        public AccountNormalBalance NormalBalance { get; set; }

        public int? ParentAccountId { get; set; }

        public string? ParentAccountCode { get; set; }

        public string? ParentAccountName { get; set; }

        public bool IsPostingAccount { get; set; }

        public bool IsControlAccount { get; set; }

        public bool IsSystemAccount { get; set; }

        public string? SystemKey { get; set; }

        public bool IsActive { get; set; }

        public string Description { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; }

        public DateTime? ModifiedDate { get; set; }

        public byte[] RowVersion { get; set; } = [];
    }
}
