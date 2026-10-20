using System.ComponentModel.DataAnnotations;

namespace VendorCreditService.API.Models
{
    public class VendorCreditApplication
    {
        public int Id { get; set; }

        public Guid RequestId { get; set; }

        [MaxLength(64)]
        public string RequestFingerprint { get; set; } = "";

        public int VendorCreditId { get; set; }

        [MaxLength(50)]
        public string VendorCreditNumber { get; set; } = "";

        public int BillId { get; set; }

        [MaxLength(50)]
        public string BillNumber { get; set; } = "";

        public decimal Amount { get; set; }

        [MaxLength(3)]
        public string Currency { get; set; } = "";

        public DateOnly ApplicationDate { get; set; }

        [MaxLength(1000)]
        public string Notes { get; set; } = "";

        public CreditApplicationStatus Status { get; set; } = CreditApplicationStatus.Applied;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? VoidedDate { get; set; }

        [MaxLength(500)]
        public string VoidReason { get; set; } = "";

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
    }
}
