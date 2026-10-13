using SalesInvoiceService.API.DTOs;

namespace SalesInvoiceService.API.Services
{
    public interface IClientApiService
    {
        Task<InvoiceClientDto?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken);
    }
}