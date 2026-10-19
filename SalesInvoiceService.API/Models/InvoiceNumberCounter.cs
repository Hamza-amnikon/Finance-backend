namespace SalesInvoiceService.API.Models
{
    public class InvoiceNumberCounter
    {
        public string PeriodKey { get; set; } = string.Empty;

        public int LastNumber { get; set; }
    }
}
