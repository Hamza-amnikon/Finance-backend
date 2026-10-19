namespace SalesEstimateService.API.DTOs
{
    public class SalesEstimateConversionResponseDto
    {
        public SalesEstimateResponseDto Estimate { get; set; } = new();

        public ConvertedInvoiceDto Invoice { get; set; } = new();
    }
}
