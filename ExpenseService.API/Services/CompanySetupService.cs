using System.ComponentModel.DataAnnotations;
using ExpenseService.API.DTOs;
using ExpenseService.API.Repositories;

namespace ExpenseService.API.Services
{
    public class CompanySetupService
    {
        private readonly CompanySetupRepository _repository;
        private readonly IExpenseLookupService _lookups;

        public CompanySetupService(
            CompanySetupRepository repository,
            IExpenseLookupService lookups)
        {
            _repository = repository;
            _lookups = lookups;
        }

        public Task<List<CompanySetupResponse>> GetAllAsync(
            CancellationToken cancellationToken)
        {
            return _repository.GetAllAsync(cancellationToken);
        }

        public async Task<CompanySetupResponse> CreateAsync(
            CompanySetupRequest request,
            CancellationToken cancellationToken)
        {
            request.CompanyName = request.CompanyName.Trim();
            request.BranchName = request.BranchName.Trim();
            request.InvoiceNumberPrefix = request.InvoiceNumberPrefix.Trim();

            Validator.ValidateObject(request, new ValidationContext(request), true);

            var country = await _lookups.ResolveCountryAsync(
                request.Country,
                cancellationToken);

            var currency = await _lookups.ResolveCurrencyAsync(
                country.CurrencyCode,
                true,
                cancellationToken);

            return await _repository.CreateAsync(
                request,
                currency.CurrencyCode,
                cancellationToken);
        }
    }
}
