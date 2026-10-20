namespace VendorCreditService.API.Models
{
    public class PurchaseBillLookup
    {
        public int Id { get; set; }

        public Guid VendorId { get; set; }

        public string? BillNumber { get; set; }

        public string VendorInvoiceNumber { get; set; } = "";

        public DateOnly BillDate { get; set; }

        public string Branch { get; set; } = "";

        public string Currency { get; set; } = "";

        public int CurrencyDecimalPlaces { get; set; }

        public decimal GrandTotal { get; set; }

        public string Status { get; set; } = "";

        public byte[] RowVersion { get; set; } = [];
    }
}
