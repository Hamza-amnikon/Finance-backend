using System.ComponentModel.DataAnnotations;
using PaymentMadeService.API.Models;
namespace PaymentMadeService.API.DTOs
{
    public class PaymentMadeResponseDto
    {
        public int Id { get; set; }

        public string PaymentNumber { get; set; } = "";

        public int BillId { get; set; }

        public string BillNumber { get; set; } = "";

        public string VendorInvoiceNumber { get; set; } = "";

        public Guid VendorId { get; set; }

        public string VendorName { get; set; } = "";

        public string Branch { get; set; } = "";

        public DateOnly PaymentDate { get; set; }

        public decimal Amount { get; set; }

        public string Currency { get; set; } = "";

        public int CurrencyDecimalPlaces { get; set; }

        public int PaymentModeId { get; set; }

        public string PaymentMode { get; set; } = "";

        public string Reference { get; set; } = "";

        public string Notes { get; set; } = "";

        public PaymentMadeStatus Status { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? VoidedDate { get; set; }

        public string VoidReason { get; set; } = "";

        public byte[] RowVersion { get; set; } = [];

        public BillPaymentBalanceDto? BillBalance { get; set; }
    }
}
