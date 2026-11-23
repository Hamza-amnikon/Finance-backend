namespace BankingService.API.Services
{
    public sealed class InternalRequestCompanyProfile
    {
        public int AccountingCompanyId { get; set; }
        public string CompanyName { get; set; } = "";
        public string Address { get; set; } = "";
        public string CountryCode { get; set; } = "";
        public string CompanyRegistrationNumber { get; set; } = "";
        public string TaxRegistrationNumber { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Email { get; set; } = "";
        public string BankAccountNumber { get; set; } = "";
        public string BankIban { get; set; } = "";
        public string BankSwiftCode { get; set; } = "";
    }
}
