using System.ComponentModel.DataAnnotations;
using PurchaseOrderService.API.Models;

namespace PurchaseOrderService.API.DTOs;

public class PurchaseOrderItemRequestDto
{
    [Required, MaxLength(200)] public string ItemName { get; set; } = "";
    [MaxLength(1000)] public string Description { get; set; } = "";
    [EnumDataType(typeof(PurchaseUnit))] public PurchaseUnit Unit { get; set; } = PurchaseUnit.Piece;
    [Range(typeof(decimal), "0.0001", "99999999999999.9999")] public decimal Quantity { get; set; } = 1;
    [Range(typeof(decimal), "0", "99999999999999.9999")] public decimal UnitPrice { get; set; }
    public bool DiscountEnabled { get; set; }
    [Range(typeof(decimal), "0", "99999999999999.9999")] public decimal DiscountAmount { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal TaxRate { get; set; }
}
