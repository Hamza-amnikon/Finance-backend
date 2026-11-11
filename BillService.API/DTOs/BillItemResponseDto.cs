using System.ComponentModel.DataAnnotations;
using BillService.API.Models;

namespace BillService.API.DTOs
{
    public class BillItemResponseDto
    {
        public int Id { get; set; }
        public int LineNumber { get; set; }
        public string ItemName { get; set; } = "";
        public string AccountCode { get; set; } = "";
        public string Description { get; set; } = "";
        public BillUnit Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public bool DiscountEnabled { get; set; }
        [System.ComponentModel.DataAnnotations.MaxLength(10)]
        public string DiscountType { get; set; } = "Fixed";

        public decimal? DiscountValue { get; set; }

        public decimal DiscountAmount { get; set; }
        public int? TaxSettingId { get; set; }
        public string TaxSettingSnapshotJson { get; set; } = "";
        public decimal TaxRate { get; set; }
        public decimal LineSubTotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal LineTotal { get; set; }
    }
}
