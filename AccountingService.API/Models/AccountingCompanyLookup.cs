using System.ComponentModel.DataAnnotations;

namespace AccountingService.API.Models
{
    public class AccountingCompanyLookup
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(160)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(2)]
        public string Country { get; set; } = string.Empty;

        [Required]
        [MaxLength(3)]
        public string Currency { get; set; } = string.Empty;

        public ICollection<Account> Accounts { get; set; }
            = new List<Account>();
    }
}