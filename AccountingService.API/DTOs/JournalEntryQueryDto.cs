using System.ComponentModel.DataAnnotations;
using AccountingService.API.Models;

namespace AccountingService.API.DTOs
{
    public class JournalEntryQueryDto
    {
        [Range(1, int.MaxValue)]
        public int CompanyId { get; set; }

        public DateOnly? FromDate { get; set; }

        public DateOnly? ToDate { get; set; }

        public JournalEntryStatus? Status { get; set; }

        public JournalEntrySource? Source { get; set; }

        [MaxLength(100)]
        public string? Branch { get; set; }

        [MaxLength(160)]
        public string? Search { get; set; }

        [Range(1, int.MaxValue)]
        public int Page { get; set; }
            = 1;

        [Range(1, 100)]
        public int PageSize { get; set; }
            = 25;
    }
}