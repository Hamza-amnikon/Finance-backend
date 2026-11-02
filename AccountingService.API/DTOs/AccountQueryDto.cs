using System.ComponentModel.DataAnnotations;
using AccountingService.API.Models;

namespace AccountingService.API.DTOs
{
    public class AccountQueryDto
    {
        [Range(1, int.MaxValue)]
        public int CompanyId { get; set; }

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 25;

        [MaxLength(160)]
        public string? Search { get; set; }

        public AccountType? Type { get; set; }

        public int? ParentAccountId { get; set; }

        public bool? IsPostingAccount { get; set; }

        public bool? IsActive { get; set; }
    }
}