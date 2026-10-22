using System.ComponentModel.DataAnnotations;

namespace BankingService.API.DTOs
{
    public class BankAccountQueryDto
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 25;

        [MaxLength(160)]
        public string? Search { get; set; }

        [Range(1, int.MaxValue)]
        public int? CompanyId { get; set; }

        [RegularExpression("^[A-Za-z]{3}$")]
        public string? Currency { get; set; }

        public bool? IsActive { get; set; }
    }
}