using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Services
{
    public interface IPaymentModeService
    {
        Task<List<PaymentMode>> GetActiveAsync(
            CancellationToken cancellationToken);

        Task<PaymentMode?> GetByNameAsync(
            string name,
            CancellationToken cancellationToken);
    }
}