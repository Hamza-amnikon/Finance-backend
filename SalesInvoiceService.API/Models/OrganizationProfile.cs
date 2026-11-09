using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesInvoiceService.API.Models
{
    [Table("OrganizationProfiles", Schema = "sales")]
    public class OrganizationProfile
    {
        public int Id { get; set; }
        [MaxLength(200)]
        public string Name { get; set; } = "";
        [MaxLength(254)]
        public string Email { get; set; } = "";
        [MaxLength(25)]
        public string Phone { get; set; } = "";
        [MaxLength(3)]
        public string Currency { get; set; } = "";
        [MaxLength(1000)]
        public string Address { get; set; } = "";
        [MaxLength(2000)]
        public string Notes { get; set; } = "";
    }
}
