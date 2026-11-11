using System.ComponentModel.DataAnnotations;
using BillService.API.Models;

namespace BillService.API.DTOs
{
    public class BillActionDto
    {
        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] RowVersion { get; set; } = [];

        [MaxLength(200)]
        public string? VendorInvoiceNumber { get; set; }

        public DateOnly? DueDate { get; set; }
    }
}
