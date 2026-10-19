namespace SalesEstimateService.API.DTOs
{
    public class ConvertedInvoiceDto
    {
        public int Id { get; set; }
        public string? InvoiceNumber { get; set; }
        public DateOnly InvoiceDate { get; set; }
        public DateOnly DueDate { get; set; }
        public string PreferredPaymentMode { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public decimal GrandTotal { get; set; }
        public int Status { get; set; }
        public int? SourceEstimateId { get; set; }
        public byte[]? SourceEstimateRowVersion { get; set; }
        public DateOnly? SourceEstimateInvoiceDate { get; set; }
        public DateOnly? SourceEstimateDueDate { get; set; }
    }
}
