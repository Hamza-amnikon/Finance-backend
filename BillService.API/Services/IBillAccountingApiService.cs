using BillService.API.DTOs;

namespace BillService.API.Services
{
    public interface IBillAccountingApiService
    {
        Task<BillAccountingCompanyDto?> GetCompanyAsync(
            int companyId,
            CancellationToken cancellationToken);

        Task<BillAccountingResultDto> ReverseBillAsync(int billId, CancellationToken cancellationToken);

        Task<BillAccountingResultDto> PostBillAsync(
            BillAccountingPostingDto posting,
            CancellationToken cancellationToken);
    }
}
