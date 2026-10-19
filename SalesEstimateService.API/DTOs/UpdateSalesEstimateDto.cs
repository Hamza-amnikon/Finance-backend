using System.ComponentModel.DataAnnotations;

namespace SalesEstimateService.API.DTOs
{
    public class UpdateSalesEstimateDto : CreateSalesEstimateDto
    {
        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] RowVersion { get; set; } = [];
    }
}
