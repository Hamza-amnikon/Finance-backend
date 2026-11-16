namespace ExpenseService.API.DTOs
{
    public class CompanySetupResponse
    {
        public int CompanyId { get; set; }

        public string CompanyName { get; set; } = string.Empty;

        public int BranchId { get; set; }

        public string BranchName { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public string Currency { get; set; } = string.Empty;

        public string InvoiceNumberPrefix { get; set; } = string.Empty;
    }
}
