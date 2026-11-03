using System.ComponentModel.DataAnnotations;

namespace AccountingService.API.DTOs
{
    public class CreateJournalEntryDto
    {
        [Required]
        public Guid RequestId { get; set; }

        [Range(1, int.MaxValue)]
        public int CompanyId { get; set; }

        public DateOnly EntryDate { get; set; }

        [Required]
        [MaxLength(100)]
        public string Branch { get; set; }
            = string.Empty;

        [Required]
        [StringLength(
            3,
            MinimumLength = 3)]
        public string Currency { get; set; }
            = string.Empty;

        [Range(
            typeof(decimal),
            "0.00000001",
            "9999999999.99999999")]
        public decimal ExchangeRate { get; set; }
            = 1m;

        [Required]
        [MaxLength(500)]
        public string Narration { get; set; }
            = string.Empty;

        [MaxLength(100)]
        public string? Reference { get; set; }

        [Required]
        [MinLength(2)]
        public List<JournalEntryLineDto> Lines { get; set; }
            = new();
    }
}