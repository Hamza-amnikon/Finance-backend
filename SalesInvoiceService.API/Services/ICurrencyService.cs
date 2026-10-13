using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Services
{
    public interface ICurrencyService
    {
        Task<List<CurrencyMaster>> GetActiveAsync(
            CancellationToken cancellationToken);

        Task<CurrencyMaster?> GetByCodeAsync(
            string currencyCode,
            CancellationToken cancellationToken);
    }
}