namespace BankingService.API.Models
{
    public class BankingAccountingAccountLookup
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }

        public string Code { get; set; } = "";

        public string Name { get; set; } = "";

        public string Type { get; set; } = "";

        public int? ParentAccountId { get; set; }

        public bool IsPostingAccount { get; set; }

        public bool IsControlAccount { get; set; }

        public bool IsSystemAccount { get; set; }

        public string? SystemKey { get; set; }

        public bool IsActive { get; set; }
    }
}
