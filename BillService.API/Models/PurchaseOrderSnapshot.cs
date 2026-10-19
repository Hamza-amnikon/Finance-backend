namespace BillService.API.Models
{
    // Unmapped query results over the existing PO tables; this service owns no PO schema.
    public class PurchaseOrderSnapshot
    {
        public int Id { get; set; }
        public string Status { get; set; } = "";
        public byte[] RowVersion { get; set; } = [];
        public string? PurchaseOrderNumber { get; set; }
        public Guid VendorId { get; set; }
        public string VendorName { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string BillingAddress { get; set; } = "";
        public string ShippingAddress { get; set; } = "";
        public string Branch { get; set; } = "";
        public string Currency { get; set; } = "";
        public int CurrencyDecimalPlaces { get; set; }
        public string PaymentTerms { get; set; } = "";
        public string Reference { get; set; } = "";
        public string Notes { get; set; } = "";
        public string AdditionalTerms { get; set; } = "";
        public decimal GrandTotal { get; set; }
    }
}
