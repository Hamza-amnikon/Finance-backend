using System.ComponentModel.DataAnnotations;

namespace SalesInvoiceService.API.Models
{
    public class PaymentMode
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}