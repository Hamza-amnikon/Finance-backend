using System.ComponentModel.DataAnnotations;

namespace ExpenseService.API.DTOs
{
    public class CompanySetupRequest
    {
        [Required]
        [MaxLength(160)]
        public string CompanyName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string BranchName { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^[A-Z]{2}$")]
        public string Country { get; set; } = string.Empty;

        [MaxLength(10)]
        [RegularExpression("^[A-Za-z0-9]*$")]
        public string InvoiceNumberPrefix { get; set; } = string.Empty;
    }
}
