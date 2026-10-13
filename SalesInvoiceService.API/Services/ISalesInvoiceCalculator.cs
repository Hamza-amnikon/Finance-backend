using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Services
{
    public interface ISalesInvoiceCalculator
    {
        void Calculate(
            SalesInvoice invoice,
            int currencyDecimalPlaces);
    }
}