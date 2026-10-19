using System.ComponentModel.DataAnnotations;

namespace VendorService.API.DTOs
{
    public class ArchiveVendorDto
    {
        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] RowVersion { get; set; } = [];
    }
}