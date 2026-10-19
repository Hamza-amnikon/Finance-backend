using System.ComponentModel.DataAnnotations;

namespace BillService.API.Models
{
    public class Bill
    {
        public int Id { get; set; }
        public Guid RequestId { get; set; }
        [MaxLength(64)] public string RequestFingerprint { get; set; } = "";
        public int? SourcePurchaseOrderId { get; set; }
        [MaxLength(50)] public string? SourcePurchaseOrderNumber { get; set; }
        [MaxLength(200)] public string VendorInvoiceNumber { get; set; } = "";
        public Guid VendorId { get; set; }
        [MaxLength(50)] public string? BillNumber { get; set; }
        [Required, MaxLength(200)] public string VendorName { get; set; } = "";
        [MaxLength(200)] public string CompanyName { get; set; } = "";
        [MaxLength(1000)] public string BillingAddress { get; set; } = "";
        [MaxLength(1000)] public string ShippingAddress { get; set; } = "";
        public DateOnly BillDate { get; set; }
        public DateOnly? DueDate { get; set; }
        [MaxLength(200)] public string Reference { get; set; } = "";
        [MaxLength(200)] public string PaymentTerms { get; set; } = "";
        [MaxLength(2000)] public string Notes { get; set; } = "";
        [MaxLength(2000)] public string AdditionalTerms { get; set; } = "";
        [Required, MaxLength(20)] public string Branch { get; set; } = "";
        [Required, MaxLength(3)] public string Currency { get; set; } = "";
        public int CurrencyDecimalPlaces { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public BillStatus Status { get; set; } = BillStatus.Draft;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedDate { get; set; }
        public DateTime? IssuedDate { get; set; }
        public DateTime? CancelledDate { get; set; }
        [Timestamp] public byte[] RowVersion { get; set; } = [];
        public ICollection<BillItem> Items { get; set; } = new List<BillItem>();
    }
}
