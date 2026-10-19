using SalesInvoiceService.API.DTOs;

namespace SalesInvoiceService.API.Services
{
    public interface IEstimateApiService
    {
        Task<EstimateForInvoiceDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
    }
}
