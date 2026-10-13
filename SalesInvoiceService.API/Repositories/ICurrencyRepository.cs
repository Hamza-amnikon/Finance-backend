using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Repositories
{
    public interface ICurrencyRepository
    {
        Task<List<CurrencyMaster>> GetActiveAsync(
            CancellationToken cancellationToken);

        Task<CurrencyMaster?> GetByCodeAsync(
            string currencyCode,
            CancellationToken cancellationToken);
    }
}