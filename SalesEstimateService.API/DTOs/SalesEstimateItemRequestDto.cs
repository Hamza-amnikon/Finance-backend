using System.ComponentModel.DataAnnotations;
using SalesEstimateService.API.Models;

namespace SalesEstimateService.API.DTOs
{
    public class SalesEstimateItemRequestDto
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

        public bool DiscountEnabled { get; set; } = false;

        [System.ComponentModel.DataAnnotations.RegularExpression("^(Fixed|Percentage)$")]
        public string DiscountType { get; set; } = "Fixed";

        [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0", "99999999999999.9999")]
        public decimal? DiscountValue { get; set; }

        [Range(typeof(decimal), "0", "99999999999999.9999")]
        public decimal DiscountAmount { get; set; }

        [Range(1, int.MaxValue)]
        public int? TaxSettingId { get; set; }

        public string TaxSettingRowVersion { get; set; } = "";
    }
}