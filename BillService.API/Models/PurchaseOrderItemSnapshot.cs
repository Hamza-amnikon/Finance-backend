namespace BillService.API.Models
{
    public class PurchaseOrderItemSnapshot
    {
        public int LineNumber { get; set; }
        public string ItemName { get; set; } = "";
        public string Description { get; set; } = "";
        public string Unit { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public bool DiscountEnabled { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxRate { get; set; }
    }
}
