using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Repositories
{
    public interface IPaymentModeRepository
    {
        Task<List<PaymentMode>> GetActiveAsync(
            CancellationToken cancellationToken);

        Task<PaymentMode?> GetByNameAsync(
            string name,
            CancellationToken cancellationToken);
    }
}