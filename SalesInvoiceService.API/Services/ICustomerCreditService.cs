using SalesInvoiceService.API.DTOs;

namespace SalesInvoiceService.API.Services
{
    public interface ICustomerCreditService
    {
        Task<CustomerCreditDto> GetAsync(
            Guid clientId,
            string branch,
            string currency,
            CancellationToken cancellationToken);

        Task<CustomerCreditApplicationDto> ApplyAsync(
            ApplyCustomerCreditDto dto,
            CancellationToken cancellationToken);
    }
}
