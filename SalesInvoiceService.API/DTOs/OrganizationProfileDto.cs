using System.ComponentModel.DataAnnotations;

namespace SalesInvoiceService.API.DTOs
{
    public class OrganizationProfileDto
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = "";
        [Required, EmailAddress, MaxLength(254)]
        public string Email { get; set; } = "";
        [MaxLength(25)]
        public string Phone { get; set; } = "";
        [Required, StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; } = "";
        [MaxLength(1000)]
        public string Address { get; set; } = "";
        [MaxLength(2000)]
        public string Notes { get; set; } = "";
    }
}
