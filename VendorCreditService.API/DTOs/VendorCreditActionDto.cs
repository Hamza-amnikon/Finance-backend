using System.ComponentModel.DataAnnotations;

namespace VendorCreditService.API.DTOs
{
    public class VendorCreditActionDto
    {
        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] RowVersion { get; set; } = [];
    }
}