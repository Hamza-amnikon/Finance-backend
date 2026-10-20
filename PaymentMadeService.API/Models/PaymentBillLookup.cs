using System.ComponentModel.DataAnnotations;

namespace PaymentMadeService.API.Models
{
    public class PaymentBillLookup
    {
        public int Id { get; set; }

        public Guid VendorId { get; set; }

        public string VendorName { get; set; } = "";

        public string? BillNumber { get; set; }

        public string VendorInvoiceNumber { get; set; } = "";

        public string Branch { get; set; } = "";

        public string Currency { get; set; } = "";

        public int CurrencyDecimalPlaces { get; set; }

        public DateOnly BillDate { get; set; }

        public decimal GrandTotal { get; set; }

        public string Status { get; set; } = "";

        public byte[] RowVersion { get; set; } = [];
    }
}
