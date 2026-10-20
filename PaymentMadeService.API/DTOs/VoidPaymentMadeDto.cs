using System.ComponentModel.DataAnnotations;

namespace PaymentMadeService.API.DTOs
{
    public class VoidPaymentMadeDto
    {
        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] RowVersion { get; set; } = [];

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = "";
    }
}
