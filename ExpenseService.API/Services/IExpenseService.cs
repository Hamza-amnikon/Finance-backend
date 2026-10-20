using ExpenseService.API.DTOs;

namespace ExpenseService.API.Services
{
    public interface IExpenseService
    {
        Task<ExpenseListDto> GetPagedAsync(
            ExpenseQueryDto query,
            CancellationToken cancellationToken);

        Task<ExpenseResponseDto> GetByIdAsync(
            int id,
            string? currency,
            CancellationToken cancellationToken);

        Task<ExpenseResponseDto> SaveAsync(
            int? id,
            ExpenseDto dto,
            CancellationToken cancellationToken);

        Task<ExpenseResponseDto> VoidAsync(
            int id,
            VoidExpenseDto dto,
            CancellationToken cancellationToken);
    }
}
