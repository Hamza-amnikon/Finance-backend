using SalesInvoiceService.API.Models;
using SalesInvoiceService.API.Repositories;

namespace SalesInvoiceService.API.Services
{
    public class CurrencyService : ICurrencyService
    {
        private readonly ICurrencyRepository _repository;

        public CurrencyService(ICurrencyRepository repository)
        {
            _repository = repository;
        }

        public Task<List<CurrencyMaster>> GetActiveAsync(
            CancellationToken cancellationToken)
        {
            return _repository.GetActiveAsync(cancellationToken);
        }

        public async Task<CurrencyMaster?> GetByCodeAsync(
            string currencyCode,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(currencyCode))
            {
                return null;
            }

            var code = currencyCode.Trim().ToUpperInvariant();

            if (code.Length != 3)
            {
                return null;
            }

            var currency = await _repository.GetByCodeAsync(
                code,
                cancellationToken);

            return currency?.IsActive == true ? currency : null;
        }
    }
}