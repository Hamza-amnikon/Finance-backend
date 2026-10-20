using System.ComponentModel.DataAnnotations;

namespace ExpenseService.API.DTOs
{
    public class CompanyDto
    {
        [Required]
        [MaxLength(160)]
        public string Name { get; set; } = "";

        [Required]
        [RegularExpression("^[A-Z]{2}$")]
        public string Country { get; set; } = "";
    }
}
