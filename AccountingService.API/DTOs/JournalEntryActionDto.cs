using System.ComponentModel.DataAnnotations;

namespace AccountingService.API.DTOs
{
    public class JournalEntryActionDto
    {
        [Required]
        public string RowVersion { get; set; }
            = string.Empty;

        [MaxLength(500)]
        public string? Reason { get; set; }
    }
}