using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Repositories
{
    public interface ISalesInvoiceRepository
    {
        Task<SalesInvoice> CreateAsync(
            SalesInvoice invoice,
            CancellationToken cancellationToken);

        Task<SalesInvoice> IssueAsync(
            SalesInvoice invoice,
            byte[] rowVersion,
            CancellationToken cancellationToken);

        Task<SalesInvoice?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken);

        Task<(List<SalesInvoice> Data, int Total)> GetAllAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken);
    }
}