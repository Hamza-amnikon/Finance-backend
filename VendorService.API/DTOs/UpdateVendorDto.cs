using System.ComponentModel.DataAnnotations;

namespace VendorService.API.DTOs
{
    public class UpdateVendorDto : CreateVendorDto
    {
        [Required]
        [MinLength(1)]
        public byte[] RowVersion { get; set; } = [];
    }
}