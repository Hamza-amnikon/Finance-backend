using System.ComponentModel.DataAnnotations;

namespace PurchaseOrderService.API.Models
{
    public class PurchaseOrder
    {
        public int Id { get; set; }

        public Guid RequestId { get; set; }

        [MaxLength(64)]
        public string RequestFingerprint { get; set; } = "";

        public Guid VendorId { get; set; }

        [MaxLength(50)]
        public string? PurchaseOrderNumber { get; set; }

        [Required]
        [MaxLength(200)]
        public string VendorName { get; set; } = "";

        [MaxLength(200)]
        public string CompanyName { get; set; } = "";

        [MaxLength(1000)]
        public string BillingAddress { get; set; } = "";

        [MaxLength(1000)]
        public string ShippingAddress { get; set; } = "";

        public DateOnly OrderDate { get; set; }

        public DateOnly? ExpectedDeliveryDate { get; set; }

        [MaxLength(200)]
        public string Reference { get; set; } = "";

        [MaxLength(200)]
        public string PaymentTerms { get; set; } = "";

        public int? PaymentModeId { get; set; }

        [MaxLength(2000)]
        public string Notes { get; set; } = "";

        [MaxLength(2000)]
        public string AdditionalTerms { get; set; } = "";

        [Required]
        [MaxLength(20)]
        public string Branch { get; set; } = "";

        [Required]
        [MaxLength(3)]
        public string Currency { get; set; } = "";

        public int CurrencyDecimalPlaces { get; set; }

        public decimal SubTotal { get; set; }

        public decimal DiscountTotal { get; set; }

        public decimal TaxTotal { get; set; }

        public decimal GrandTotal { get; set; }

        public PurchaseOrderStatus Status { get; set; } =
            PurchaseOrderStatus.Draft;

        public DateTime CreatedDate { get; set; } =
            DateTime.UtcNow;

        public DateTime? ModifiedDate { get; set; }

        public DateTime? IssuedDate { get; set; }

        public DateTime? CancelledDate { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];

        public ICollection<PurchaseOrderItem> Items { get; set; } =
            new List<PurchaseOrderItem>();
    }
}