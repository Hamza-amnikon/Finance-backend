using System.ComponentModel.DataAnnotations;

namespace VendorCreditService.API.Models
{
    public class VendorCredit
    {
        public int Id { get; set; }

        public Guid RequestId { get; set; }

        [MaxLength(64)]
        public string RequestFingerprint { get; set; } = "";

        public Guid VendorId { get; set; }

        public int? BillId { get; set; }

        [MaxLength(50)]
        public string? VendorCreditNumber { get; set; }

        [MaxLength(200)]
        public string VendorCreditNoteNumber { get; set; } = "";

        [Required]
        [MaxLength(200)]
        public string VendorName { get; set; } = "";

        [MaxLength(200)]
        public string CompanyName { get; set; } = "";

        [MaxLength(1000)]
        public string BillingAddress { get; set; } = "";

        public DateOnly CreditDate { get; set; }

        [MaxLength(200)]
        public string Reference { get; set; } = "";

        [MaxLength(1000)]
        public string Reason { get; set; } = "";

        [MaxLength(2000)]
        public string Notes { get; set; } = "";

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

        public VendorCreditStatus Status { get; set; } =
            VendorCreditStatus.Draft;

        public DateTime CreatedDate { get; set; } =
            DateTime.UtcNow;

        public DateTime? ModifiedDate { get; set; }

        public DateTime? IssuedDate { get; set; }

        public DateTime? CancelledDate { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];

        public ICollection<VendorCreditItem> Items { get; set; } =
            new List<VendorCreditItem>();
    }
}