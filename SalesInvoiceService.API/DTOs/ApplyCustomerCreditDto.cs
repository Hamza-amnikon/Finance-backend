using System.ComponentModel.DataAnnotations;

namespace SalesInvoiceService.API.DTOs
{
    public class ApplyCustomerCreditDto
    {
        [Required]
        public Guid RequestId { get; set; }

        [Range(1, int.MaxValue)]
        public int SourceInvoiceId { get; set; }

        [Range(1, int.MaxValue)]
        public int TargetInvoiceId { get; set; }

        [Range(
            typeof(decimal),
            "0.0001",
            "99999999999999.9999")]
        public decimal Amount { get; set; }
    }
}
