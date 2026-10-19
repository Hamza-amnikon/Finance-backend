namespace SalesInvoiceService.API.DTOs
{
    public class EstimateForInvoiceDto
    {
        public int Id { get; set; }
        public Guid ClientId { get; set; }
        public string? EstimateNumber { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string BillingAddress { get; set; } = string.Empty;
        public DateOnly? ServicePeriodStart { get; set; }
        public DateOnly? ServicePeriodEnd { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public decimal SubTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal GrandTotal { get; set; }
        // Estimate API status: Accepted = 2.
        public int Status { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string AdditionalTerms { get; set; } = string.Empty;
        public byte[] RowVersion { get; set; } = [];
        public List<SalesInvoiceItemResponseDto> Items { get; set; } = new();
    }
}
