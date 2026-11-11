using System.ComponentModel.DataAnnotations;
using BillService.API.Models;

namespace BillService.API.DTOs
{
    public class BillItemRequestDto
    {
        [Required, MaxLength(200)]
        public string ItemName { get; set; } = "";
        [Required, MaxLength(30)]
        public string AccountCode { get; set; } = "";
        [MaxLength(1000)]
        public string Description { get; set; } = "";
        [EnumDataType(typeof(BillUnit))]
        public BillUnit Unit { get; set; } = BillUnit.Piece;
        [Range(typeof(decimal), "0.0001", "99999999999999.9999")]
        public decimal Quantity { get; set; } = 1;
        [Range(typeof(decimal), "0", "99999999999999.9999")]
        public decimal UnitPrice { get; set; }
        public bool DiscountEnabled { get; set; }
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
