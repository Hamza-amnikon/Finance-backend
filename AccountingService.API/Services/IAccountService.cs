using AccountingService.API.DTOs;
using AccountingService.API.Models;

namespace AccountingService.API.Services
{
    public interface IAccountService
    {
        Task<AccountListDto> GetPagedAsync(
            AccountQueryDto query,
            CancellationToken cancellationToken);

        Task<AccountResponseDto?> GetByIdAsync(
            int id,
            int companyId,
            CancellationToken cancellationToken);

        Task<AccountResponseDto> CreateAsync(
            CreateAccountDto dto,
            CancellationToken cancellationToken);

        Task<AccountResponseDto?> UpdateAsync(
            int id,
            int companyId,
            UpdateAccountDto dto,
            CancellationToken cancellationToken);

        Task<List<AccountingCompanyLookup>> GetCompaniesAsync(
            CancellationToken cancellationToken);

        Task<IReadOnlyList<AccountResponseDto>> GetTreeAsync(
            int companyId,
            CancellationToken cancellationToken);
    }
}
