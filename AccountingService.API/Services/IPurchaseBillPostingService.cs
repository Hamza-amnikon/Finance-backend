using AccountingService.API.DTOs;

namespace AccountingService.API.Services
{
    public interface IPurchaseBillPostingService
    {
        Task<PurchaseBillPostingResponseDto> PostAsync(
            PostPurchaseBillDto dto,
            CancellationToken cancellationToken);
    }
}
