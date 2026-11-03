using System.ComponentModel.DataAnnotations;

namespace AccountingService.API.DTOs
{
    public class JournalEntryLineDto
    {
        [Range(1, int.MaxValue)]
        public int AccountId { get; set; }

        [MaxLength(500)]
        public string Description { get; set; }
            = string.Empty;

        [Range(
            typeof(decimal),
            "0",
            "999999999999999.9999")]
        public decimal DebitAmount { get; set; }

        [Range(
            typeof(decimal),
            "0",
            "999999999999999.9999")]
        public decimal CreditAmount { get; set; }

        [MaxLength(100)]
        public string? CostCenter { get; set; }

        [MaxLength(50)]
        public string? CounterpartyType { get; set; }

        [MaxLength(100)]
        public string? CounterpartyId { get; set; }
    }
}