using SalesInvoiceService.API.Models;
using SalesInvoiceService.API.Repositories;

namespace SalesInvoiceService.API.Services
{
    public class PaymentModeService : IPaymentModeService
    {
        private readonly IPaymentModeRepository _repository;

        public PaymentModeService(
            IPaymentModeRepository repository)
        {
            _repository = repository;
        }

        public Task<List<PaymentMode>> GetActiveAsync(
            CancellationToken cancellationToken)
        {
            return _repository.GetActiveAsync(cancellationToken);
        }

        public async Task<PaymentMode?> GetByNameAsync(
            string name,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var value = name.Trim();

            if (value.Length > 50)
            {
                return null;
            }

            return await _repository.GetByNameAsync(
                value,
                cancellationToken);
        }
    }
}