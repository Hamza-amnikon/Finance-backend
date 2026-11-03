using AccountingService.API.DTOs;

namespace AccountingService.API.Services
{
    public interface IJournalEntryService
    {
        Task<JournalEntryPagedResultDto> GetPagedAsync(
            JournalEntryQueryDto query,
            CancellationToken cancellationToken);

        Task<JournalEntryResponseDto?> GetByIdAsync(
            int id,
            int companyId,
            CancellationToken cancellationToken);

        Task<JournalEntryResponseDto> CreateAsync(
            CreateJournalEntryDto dto,
            CancellationToken cancellationToken);

        Task<JournalEntryResponseDto?> UpdateAsync(
            int id,
            int companyId,
            UpdateJournalEntryDto dto,
            CancellationToken cancellationToken);

        Task<JournalEntryResponseDto?> PostAsync(
            int id,
            int companyId,
            JournalEntryActionDto dto,
            CancellationToken cancellationToken);

        Task<JournalEntryResponseDto?> ReverseAsync(
            int id,
            int companyId,
            JournalEntryActionDto dto,
            CancellationToken cancellationToken);
    }
}