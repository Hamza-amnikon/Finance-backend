using System.ComponentModel.DataAnnotations;

namespace ExpenseService.API.Models
{
    public class Company
    {
        public int Id { get; set; }

        [MaxLength(160)]
        public string Name { get; set; } = "";

        [MaxLength(2)]
        public string Country { get; set; } = "";

        [MaxLength(3)]
        public string Currency { get; set; } = "";
    }
}
