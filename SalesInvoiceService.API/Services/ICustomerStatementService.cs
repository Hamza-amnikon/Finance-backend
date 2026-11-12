using SalesInvoiceService.API.DTOs;

namespace SalesInvoiceService.API.Services
{
    public interface ICustomerStatementService
    {
        Task<List<CustomerStatementClientDto>> GetClientsAsync(
            CancellationToken cancellationToken);

        Task<CustomerStatementDto> GetAsync(
            Guid clientId,
            string branch,
            string currency,
            CancellationToken cancellationToken,
            DateOnly? fromDate = null,
            DateOnly? toDate = null);
    }
}
