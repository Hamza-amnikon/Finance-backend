using System.ComponentModel.DataAnnotations;
using SalesEstimateService.API.Models;

namespace SalesEstimateService.API.DTOs
{
    public class SalesEstimateQueryDto
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 25;

        [MaxLength(200)]
        public string? Search { get; set; }

        [EnumDataType(typeof(SalesEstimateStatus))]
        public SalesEstimateStatus? Status { get; set; }
    }
}
