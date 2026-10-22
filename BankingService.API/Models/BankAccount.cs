using System.ComponentModel.DataAnnotations;

namespace BankingService.API.Models
{
    public class BankAccount
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }

        [Required]
        [MaxLength(160)]
        public string Name { get; set; } = "";

        [MaxLength(160)]
        public string BankName { get; set; } = "";

        [MaxLength(50)]
        public string AccountNumber { get; set; } = "";

        [MaxLength(4)]
        public string LastFour { get; set; } = "";

        [Required]
        [MaxLength(3)]
        public string Currency { get; set; } = "";

        public int CurrencyDecimalPlaces { get; set; }

        public decimal OpeningBalance { get; set; }

        public DateOnly? OpeningBalanceDate { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ModifiedDate { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
    }
}