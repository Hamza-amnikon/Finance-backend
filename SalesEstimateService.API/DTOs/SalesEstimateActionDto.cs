using System.ComponentModel.DataAnnotations;

namespace SalesEstimateService.API.DTOs
{
    public class SalesEstimateActionDto
    {
        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] RowVersion { get; set; } = [];
    }
}
