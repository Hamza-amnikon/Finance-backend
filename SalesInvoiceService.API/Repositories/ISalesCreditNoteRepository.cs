using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Repositories
{
    public interface ISalesCreditNoteRepository
    {
        Task<SalesInvoice?> GetInvoiceAsync(
            int salesInvoiceId,
            CancellationToken cancellationToken);

        Task<List<SalesCreditNoteItem>> GetIssuedCreditItemsAsync(
            int salesInvoiceId,
            CancellationToken cancellationToken);

        Task<SalesCreditNote?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken);

        Task<SalesCreditNote?> GetByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken);

        Task<(List<SalesCreditNote> Items, int Total)> GetPagedAsync(
            int page,
            int pageSize,
            string? search,
            SalesCreditNoteStatus? status,
            CancellationToken cancellationToken);

        Task<SalesCreditNote> CreateDraftAsync(
            SalesCreditNote creditNote,
            CancellationToken cancellationToken);

        Task<SalesCreditNote?> IssueAsync(
            int id,
            byte[] rowVersion,
            CancellationToken cancellationToken);
    }
}