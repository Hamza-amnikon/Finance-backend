using System.ComponentModel.DataAnnotations;

namespace BankingService.API.DTOs
{
    public class BankTransactionVersionDto
    {
        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] RowVersion { get; set; } = [];
    }
}