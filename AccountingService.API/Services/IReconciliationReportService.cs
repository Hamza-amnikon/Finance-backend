using AccountingService.API.DTOs;

namespace AccountingService.API.Services
{
    public interface IReconciliationReportService
    {
        Task<ReconciliationReportDto> GetAsync(int companyId, DateOnly asOf, CancellationToken cancellationToken);
    }
}
