using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Repositories
{
    public interface ISalesInvoiceRepository
    {
        Task<SalesInvoice> CreateAsync(
            SalesInvoice invoice,
            CancellationToken cancellationToken);

        Task<SalesInvoice?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken);
    }
}