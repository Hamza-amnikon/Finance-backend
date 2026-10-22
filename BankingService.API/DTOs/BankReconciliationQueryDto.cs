using System.ComponentModel.DataAnnotations;

namespace BankingService.API.DTOs
{
    public class BankReconciliationQueryDto
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 25;

        [MaxLength(200)]
        public string? Search { get; set; }

        [Range(1, int.MaxValue)]
        public int? BankAccountId { get; set; }

        [Range(1, int.MaxValue)]
        public int? CompanyId { get; set; }

        [RegularExpression("^(Draft|Completed)$")]
        public string? Status { get; set; }
    }
}