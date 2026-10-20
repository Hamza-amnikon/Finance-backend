using System.ComponentModel.DataAnnotations;

namespace PaymentMadeService.API.Models
{
    public class PaymentMade
    {
        public int Id { get; set; }

        public Guid RequestId { get; set; }

        [MaxLength(64)]
        public string RequestFingerprint { get; set; } = "";

        [Required]
        [MaxLength(50)]
        public string PaymentNumber { get; set; } = "";

        public int BillId { get; set; }

        [Required]
        [MaxLength(50)]
        public string BillNumber { get; set; } = "";

        [MaxLength(200)]
        public string VendorInvoiceNumber { get; set; } = "";

        public Guid VendorId { get; set; }

        [Required]
        [MaxLength(200)]
        public string VendorName { get; set; } = "";

        [Required]
        [MaxLength(20)]
        public string Branch { get; set; } = "";

        public DateOnly PaymentDate { get; set; }

        public decimal Amount { get; set; }

        [Required]
        [MaxLength(3)]
        public string Currency { get; set; } = "";

        public int CurrencyDecimalPlaces { get; set; }

        public int PaymentModeId { get; set; }

        [Required]
        [MaxLength(50)]
        public string PaymentMode { get; set; } = "";

        [MaxLength(200)]
        public string Reference { get; set; } = "";

        [MaxLength(2000)]
        public string Notes { get; set; } = "";

        public PaymentMadeStatus Status { get; set; } = PaymentMadeStatus.Recorded;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? VoidedDate { get; set; }

        [MaxLength(500)]
        public string VoidReason { get; set; } = "";

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
    }
}
