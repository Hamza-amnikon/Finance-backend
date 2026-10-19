namespace BillService.API.Models
{
    public class PurchaseVendorLookup
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string Address { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string Country { get; set; } = "";
        public string PostalCode { get; set; } = "";
        public string Branch { get; set; } = "";
        public string Currency { get; set; } = "";
        public string PaymentTerms { get; set; } = "";
        public bool IsActive { get; set; }
    }
}
