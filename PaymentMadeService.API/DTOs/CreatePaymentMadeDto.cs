using System.ComponentModel.DataAnnotations;

namespace PaymentMadeService.API.DTOs
{
    public class CreatePaymentMadeDto
    {
        public Guid RequestId { get; set; }

        public Guid VendorId { get; set; }

        [Range(1, int.MaxValue)]
        public int BillId { get; set; }

        public DateOnly PaymentDate { get; set; }

        public decimal Amount { get; set; }

        [Range(1, int.MaxValue)]
        public int PaymentModeId { get; set; }

        [MaxLength(200)]
        public string Reference { get; set; } = "";

        [MaxLength(2000)]
        public string Notes { get; set; } = "";

        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] BillRowVersion { get; set; } = [];
    }
}
