using AccountingService.API.DTOs;

namespace AccountingService.API.Services
{
    public interface ISalesCreditNotePostingService
    {
        Task<SalesCreditNotePostingResponseDto> PostAsync(
            PostSalesCreditNoteDto dto,
            CancellationToken cancellationToken);
    }
}
