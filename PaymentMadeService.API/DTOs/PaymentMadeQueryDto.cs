using System.ComponentModel.DataAnnotations;
using PaymentMadeService.API.Models;
namespace PaymentMadeService.API.DTOs
{
    public class PaymentMadeQueryDto
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 25;

        [MaxLength(200)]
        public string? Search { get; set; }

        [EnumDataType(typeof(PaymentMadeStatus))]
        public PaymentMadeStatus? Status { get; set; }

        public Guid? VendorId { get; set; }

        [Range(1, int.MaxValue)]
        public int? BillId { get; set; }

        public DateOnly? FromDate { get; set; }

        public DateOnly? ToDate { get; set; }
    }
}
