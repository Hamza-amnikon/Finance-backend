namespace BankingService.API.Services
{
    public class PaymentBankingOptions
    {
        public bool Enabled { get; set; }
        public List<PaymentBankingRoute> Routes { get; set; } = [];
        public List<string> CashModes { get; set; } = [];
        public List<string> DraftModes { get; set; } = [];
        public List<string> PostedModes { get; set; } = [];
    }

    public class PaymentBankingRoute
    {
        public List<string> Branches { get; set; } = [];
        public string Currency { get; set; } = "";
        public string BankAccountName { get; set; } = "";
        public string TimeZoneId { get; set; } = "";
    }
}
