using System.ComponentModel.DataAnnotations;
using AccountingService.API.Models;

namespace AccountingService.API.DTOs
{
    public class CreateAccountDto
    {
        [Range(1, int.MaxValue)]
        public int CompanyId { get; set; }

        [Required]
        [MaxLength(30)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(160)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public AccountType? Type { get; set; }

        [Required]
        public AccountNormalBalance? NormalBalance { get; set; }

        [Range(1, int.MaxValue)]
        public int? ParentAccountId { get; set; }

        public bool IsPostingAccount { get; set; } = true;

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;
    }
}