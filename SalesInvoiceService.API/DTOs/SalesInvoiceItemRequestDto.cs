using System.ComponentModel.DataAnnotations;
using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.DTOs
{
    public class SalesInvoiceItemRequestDto
    {
        [Required]
        [MaxLength(200)]
        public string ServiceName { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        [EnumDataType(typeof(ServiceUnit))]
        public ServiceUnit Unit { get; set; } =
            ServiceUnit.FixedFee;

        [Range(
            typeof(decimal),
            "0.0001",
            "99999999999999.9999",
            ErrorMessage = "Quantity must be positive and within the supported range.")]
        public decimal Quantity { get; set; } = 1;

        [Range(typeof(decimal), "0", "99999999999999.9999")]
        public decimal UnitPrice { get; set; }

        public bool DiscountEnabled { get; set; } = true;

        [Range(typeof(decimal), "0", "99999999999999.9999")]
        public decimal DiscountAmount { get; set; }

        [Range(typeof(decimal), "0", "100")]
        public decimal TaxRate { get; set; }
    }
}