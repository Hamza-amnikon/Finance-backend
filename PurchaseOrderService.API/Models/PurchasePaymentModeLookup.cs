using System.ComponentModel.DataAnnotations;

namespace PurchaseOrderService.API.Models
{
    public class PurchasePaymentModeLookup
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}