using AccountingService.API.DTOs;

namespace AccountingService.API.Services
{
    public interface IFinancialReportService
    {
        Task<FinancialReportDto> GetAsync(
            int companyId,
            DateOnly fromDate,
            DateOnly toDate,
            CancellationToken cancellationToken);
    }
}
