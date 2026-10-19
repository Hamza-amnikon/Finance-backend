using System.ComponentModel.DataAnnotations;
using BillService.API.Models;

namespace BillService.API.DTOs
{
    public class BillItemRequestDto
    {
        [Required, MaxLength(200)] public string ItemName { get; set; } = "";
        [MaxLength(1000)] public string Description { get; set; } = "";
        [EnumDataType(typeof(BillUnit))] public BillUnit Unit { get; set; } = BillUnit.Piece;
        [Range(typeof(decimal), "0.0001", "99999999999999.9999")] public decimal Quantity { get; set; } = 1;
        [Range(typeof(decimal), "0", "99999999999999.9999")] public decimal UnitPrice { get; set; }
        public bool DiscountEnabled { get; set; }
        [Range(typeof(decimal), "0", "99999999999999.9999")] public decimal DiscountAmount { get; set; }
        [Range(typeof(decimal), "0", "100")] public decimal TaxRate { get; set; }
    }
}
