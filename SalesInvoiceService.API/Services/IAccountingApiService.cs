using SalesInvoiceService.API.DTOs;

namespace SalesInvoiceService.API.Services
{
    public interface IAccountingApiService
    {
        Task<AccountingCompanyDto?>
            GetCompanyAsync(
                int companyId,
                CancellationToken cancellationToken);

        Task<SalesInvoiceAccountingResultDto>
            PostSalesInvoiceAsync(
                SalesInvoiceAccountingPostingDto posting,
                CancellationToken cancellationToken);
    }
}
