using System.ComponentModel.DataAnnotations;
using AccountingService.API.Models;

namespace AccountingService.API.DTOs
{
    public class UpdateAccountDto
    {
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

        public bool IsPostingAccount { get; set; }

        public bool IsActive { get; set; }

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] RowVersion { get; set; } = [];
    }
}