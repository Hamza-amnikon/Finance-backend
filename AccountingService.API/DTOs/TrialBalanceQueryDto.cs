using System.ComponentModel.DataAnnotations;

namespace AccountingService.API.DTOs
{
    public class TrialBalanceQueryDto
    {
        [Range(
            1,
            int.MaxValue)]
        public int CompanyId { get; set; }

        public DateOnly? AsAtDate { get; set; }

        [MaxLength(100)]
        public string? Branch { get; set; }

        [MaxLength(100)]
        public string? CostCenter { get; set; }

        public bool IncludeZeroBalances { get; set; }

        [MaxLength(160)]
        public string? Search { get; set; }

        [Range(
            1,
            int.MaxValue)]
        public int Page { get; set; }
            = 1;

        [Range(
            1,
            100)]
        public int PageSize { get; set; }
            = 25;
    }
}