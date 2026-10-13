using System.ComponentModel.DataAnnotations;

namespace SalesInvoiceService.API.Models
{
    public class CurrencyMaster
    {
        [Key]
        [MaxLength(3)]
        public string CurrencyCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string CurrencyName { get; set; } = string.Empty;

        [Range(0, 4)]
        public int DecimalPlaces { get; set; }

        public bool IsActive { get; set; } = true;
    }
}