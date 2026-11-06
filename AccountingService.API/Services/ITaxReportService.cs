using AccountingService.API.DTOs;

namespace AccountingService.API.Services
{
    public interface ITaxReportService
    {
        Task<TaxReportDto> GetAsync(
            int companyId,
            DateOnly fromDate,
            DateOnly toDate,
            CancellationToken cancellationToken);
    }
}
