using System.ComponentModel.DataAnnotations;

namespace VendorCreditService.API.DTOs
{
    public class ApplyVendorCreditDto
    {
        public Guid RequestId { get; set; }

        [Range(1, int.MaxValue)]
        public int BillId { get; set; }

        public decimal Amount { get; set; }

        public DateOnly ApplicationDate { get; set; }

        [MaxLength(1000)]
        public string Notes { get; set; } = "";

        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] CreditRowVersion { get; set; } = [];

        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] BillRowVersion { get; set; } = [];
    }
}
