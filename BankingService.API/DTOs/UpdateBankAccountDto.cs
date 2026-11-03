using System.ComponentModel.DataAnnotations;

namespace BankingService.API.DTOs
{
    public class UpdateBankAccountDto
    {
        [Range(1, int.MaxValue)]
        public int AccountingAccountId { get; set; }

        [Required]
        [MaxLength(160)]
        public string Name { get; set; } = "";

        [Required]
        [MaxLength(160)]
        public string BankName { get; set; } = "";

        public bool IsActive { get; set; }

        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] RowVersion { get; set; } = [];
    }
}
