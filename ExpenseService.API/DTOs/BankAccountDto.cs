using System.ComponentModel.DataAnnotations;

namespace ExpenseService.API.DTOs
{
    public class BankAccountDto
    {
        [Range(1, int.MaxValue)]
        public int CompanyId { get; set; }

        [Required]
        [MaxLength(160)]
        public string Name { get; set; } = "";

        [Required]
        [RegularExpression("^[A-Z]{3}$")]
        public string Currency { get; set; } = "";

        [RegularExpression("^([0-9]{4})?$")]
        public string LastFour { get; set; } = "";
    }
}
