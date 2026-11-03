namespace SalesInvoiceService.API.DTOs
{
    public class SalesInvoiceExchangeRateDto
    {
        public string Base { get; set; }
            = string.Empty;

        public string Quote { get; set; }
            = string.Empty;

        public DateOnly Date { get; set; }

        public decimal Rate { get; set; }

        public string Source { get; set; }
            = string.Empty;
    }
}
