using ExpenseService.API.DTOs;

namespace ExpenseService.API.Services
{
    public interface IExpenseExchangeRateService
    {
        Task<ExpenseExchangeRateDto> GetRateAsync(
            string baseCurrency,
            string quoteCurrency,
            DateOnly date,
            CancellationToken cancellationToken);
    }
}
