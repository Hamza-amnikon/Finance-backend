namespace VendorService.API.Models
{
    // Read model for the existing shared location table.
    public class CountryMasterLookup
    {
        public string CountryName { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = string.Empty;
    }
}
