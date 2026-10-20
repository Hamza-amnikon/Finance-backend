using System.ComponentModel.DataAnnotations;

namespace ExpenseService.API.Models
{
    public class BankAccount
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }

        public Company Company { get; set; } = null!;

        [MaxLength(160)]
        public string Name { get; set; } = "";

        [MaxLength(3)]
        public string Currency { get; set; } = "";

        [MaxLength(4)]
        public string LastFour { get; set; } = "";
    }
}
