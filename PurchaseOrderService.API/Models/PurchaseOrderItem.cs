using System.ComponentModel.DataAnnotations;

namespace PurchaseOrderService.API.Models;

public class PurchaseOrderItem
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public int LineNumber { get; set; }
    [Required, MaxLength(200)] public string ItemName { get; set; } = "";
    [MaxLength(1000)] public string Description { get; set; } = "";
    public PurchaseUnit Unit { get; set; } = PurchaseUnit.Piece;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public bool DiscountEnabled { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal LineSubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
}
