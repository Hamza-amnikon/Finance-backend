using BankingService.API.DTOs;
using BankingService.API.Models;

namespace BankingService.API.Services
{
    public interface IBankAccountService
    {
        Task<BankAccountListDto> GetPagedAsync(
            BankAccountQueryDto query,
            CancellationToken cancellationToken);

        Task<BankAccountResponseDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken);

        Task<BankAccountResponseDto> CreateAsync(
            CreateBankAccountDto dto,
            CancellationToken cancellationToken);

        Task<BankAccountResponseDto?> UpdateAsync(
            int id,
            UpdateBankAccountDto dto,
            CancellationToken cancellationToken);

        Task<BankAccountResponseDto?> ConfigureOpeningBalanceAsync(
            int id,
            ConfigureOpeningBalanceDto dto,
            CancellationToken cancellationToken);

        Task<List<BankingCompanyLookup>> GetCompaniesAsync(
            CancellationToken cancellationToken);

        Task<List<BankingCurrencyLookup>> GetCurrenciesAsync(
            CancellationToken cancellationToken);

        Task<BankAccountBalanceDto?> GetBalanceAsync(
          int id,
          CancellationToken cancellationToken);

    }
}