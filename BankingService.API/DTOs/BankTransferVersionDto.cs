using System.ComponentModel.DataAnnotations;

namespace BankingService.API.DTOs
{
    public class BankTransferVersionDto
    {
        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] RowVersion { get; set; } = [];
    }
}