using VendorCreditService.API.DTOs;

namespace VendorCreditService.API.Services
{
    public interface ICreditApplicationService
    {
        Task<(List<CreditBillBalanceDto> Data, int Total)> GetBillsAsync(
            int creditId,
            string? search,
            int page,
            int pageSize,
            CancellationToken cancellationToken);

        Task<CreditApplicationResponseDto> ApplyAsync(
            int creditId,
            ApplyVendorCreditDto dto,
            CancellationToken cancellationToken);

        Task<CreditApplicationResponseDto?> VoidAsync(
            int creditId,
            int applicationId,
            VoidCreditApplicationDto dto,
            CancellationToken cancellationToken);
    }
}
