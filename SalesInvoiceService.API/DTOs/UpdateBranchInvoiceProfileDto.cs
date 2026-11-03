using System.ComponentModel.DataAnnotations;

namespace SalesInvoiceService.API.DTOs
{
    public class UpdateBranchInvoiceProfileDto : BranchInvoiceProfileFieldsDto
    {
        [Range(1, int.MaxValue)]
        public int AccountingCompanyId { get; set; }

        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] RowVersion { get; set; } = [];
    }
}
