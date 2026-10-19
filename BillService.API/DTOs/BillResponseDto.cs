using System.ComponentModel.DataAnnotations;
using BillService.API.Models;

namespace BillService.API.DTOs
{
    public class BillResponseDto
    {
        public int Id { get; set; }
        public Guid RequestId { get; set; }
        public int? SourcePurchaseOrderId { get; set; }
        [MaxLength(50)] public string? SourcePurchaseOrderNumber { get; set; }
        [MaxLength(200)] public string VendorInvoiceNumber { get; set; } = "";
        public Guid VendorId { get; set; }
        public string? BillNumber { get; set; }
        public string VendorName { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string BillingAddress { get; set; } = "";
        public string ShippingAddress { get; set; } = "";
        public DateOnly BillDate { get; set; }
        public DateOnly? DueDate { get; set; }
        public string Reference { get; set; } = "";
        public string PaymentTerms { get; set; } = "";
        public string Notes { get; set; } = "";
        public string AdditionalTerms { get; set; } = "";
        public string Branch { get; set; } = "";
        public string Currency { get; set; } = "";
        public int CurrencyDecimalPlaces { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public BillStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? IssuedDate { get; set; }
        public DateTime? CancelledDate { get; set; }
        public byte[] RowVersion { get; set; } = [];
        public List<BillItemResponseDto> Items { get; set; } = [];
    }
}
