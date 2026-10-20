using System.ComponentModel.DataAnnotations;

namespace ExpenseService.API.DTOs
{
    public class ExpenseQueryDto
    {
        [Range(1, int.MaxValue)]
        public int? CompanyId { get; set; }

        [RegularExpression("^[A-Z]{3}$")]
        public string? DisplayCurrency { get; set; }

        [MaxLength(200)]
        public string? Search { get; set; }

        [RegularExpression("^(Draft|Recorded|Voided)$")]
        public string? Status { get; set; }

        public DateOnly? From { get; set; }

        public DateOnly? To { get; set; }

        [Range(1, 1000000)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 25;
    }
}
