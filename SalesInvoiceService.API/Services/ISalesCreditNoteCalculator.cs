using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Services
{
    public interface ISalesCreditNoteCalculator
    {
        void Calculate(
            SalesCreditNote creditNote,
            SalesInvoice originalInvoice,
            IReadOnlyCollection<SalesCreditNoteItem> issuedCreditItems,
            int decimalPlaces);
    }
}