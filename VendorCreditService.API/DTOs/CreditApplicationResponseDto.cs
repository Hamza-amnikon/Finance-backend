using System.ComponentModel.DataAnnotations;
using VendorCreditService.API.Models;
namespace VendorCreditService.API.DTOs
{
    public class CreditApplicationResponseDto
    {
        public int Id { get; set; }

        public int VendorCreditId { get; set; }

        public string VendorCreditNumber { get; set; } = "";

        public int BillId { get; set; }

        public string BillNumber { get; set; } = "";

        public decimal Amount { get; set; }

        public string Currency { get; set; } = "";

        public DateOnly ApplicationDate { get; set; }

        public string Notes { get; set; } = "";

        public CreditApplicationStatus Status { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? VoidedDate { get; set; }

        public string VoidReason { get; set; } = "";

        public byte[] RowVersion { get; set; } = [];
    }
}
