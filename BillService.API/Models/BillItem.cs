using System.ComponentModel.DataAnnotations;

namespace BillService.API.Models
{
    public class BillItem
    {
        public int Id { get; set; }
        public int BillId { get; set; }
        public Bill Bill { get; set; } = null!;
        public int LineNumber { get; set; }
        [Required, MaxLength(200)] public string ItemName { get; set; } = "";
        [MaxLength(1000)] public string Description { get; set; } = "";
        public BillUnit Unit { get; set; } = BillUnit.Piece;
        public decimal Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public bool DiscountEnabled { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxRate { get; set; }
        public decimal LineSubTotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal LineTotal { get; set; }
    }
}
