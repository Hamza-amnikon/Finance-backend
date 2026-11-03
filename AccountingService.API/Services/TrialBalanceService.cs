using System.ComponentModel.DataAnnotations;
using AccountingService.API.DTOs;
using AccountingService.API.Repositories;

namespace AccountingService.API.Services
{
    public class TrialBalanceService
        : ITrialBalanceService
    {
        private readonly ITrialBalanceRepository
            _repository;

        public TrialBalanceService(
            ITrialBalanceRepository repository)
        {
            _repository = repository;
        }

        public async Task<TrialBalanceResultDto>
            GetAsync(
                TrialBalanceQueryDto query,
                CancellationToken cancellationToken)
        {
            if (query == null)
            {
                throw new ValidationException(
                    "Trial Balance query is required.");
            }

            if (query.CompanyId <= 0)
            {
                throw new ValidationException(
                    "A valid company is required.");
            }

            if (query.Page <= 0)
            {
                throw new ValidationException(
                    "Page must be greater than zero.");
            }

            if (
                query.PageSize <= 0 ||
                query.PageSize > 100
            )
            {
                throw new ValidationException(
                    "Page size must be between 1 and 100.");
            }

            query.AsAtDate ??=
                DateOnly.FromDateTime(
                    DateTime.UtcNow);

            query.Branch =
                NormalizeOptional(
                    query.Branch);

            query.CostCenter =
                NormalizeOptional(
                    query.CostCenter);

            query.Search =
                NormalizeOptional(
                    query.Search);

            var company =
                await _repository
                    .GetCompanyAsync(
                        query.CompanyId,
                        cancellationToken);

            if (company == null)
            {
                throw new ValidationException(
                    "The selected company was not found.");
            }

            var page =
                await _repository
                    .GetPageAsync(
                        query,
                        cancellationToken);

            var difference =
                Math.Round(
                    page.TotalDebitBalance -
                    page.TotalCreditBalance,
                    4,
                    MidpointRounding.AwayFromZero);

            return new TrialBalanceResultDto
            {
                CompanyId =
                    company.Id,

                CompanyName =
                    company.Name,

                Country =
                    company.Country,

                BaseCurrency =
                    company.Currency,

                AsAtDate =
                    query.AsAtDate.Value,

                Branch =
                    query.Branch,

                CostCenter =
                    query.CostCenter,

                TotalDebitMovement =
                    page.TotalDebitMovement,

                TotalCreditMovement =
                    page.TotalCreditMovement,

                TotalDebitBalance =
                    page.TotalDebitBalance,

                TotalCreditBalance =
                    page.TotalCreditBalance,

                Difference =
                    difference,

                IsBalanced =
                    difference == 0m,

                Data =
                    page.Rows
                        .Select(row =>
                            new TrialBalanceLineDto
                            {
                                AccountId =
                                    row.AccountId,

                                AccountCode =
                                    row.AccountCode,

                                AccountName =
                                    row.AccountName,

                                AccountType =
                                    row.AccountType
                                        .ToString(),

                                NormalBalance =
                                    row.NormalBalance
                                        .ToString(),

                                ParentAccountId =
                                    row.ParentAccountId,

                                ParentAccountCode =
                                    row.ParentAccountCode,

                                ParentAccountName =
                                    row.ParentAccountName,

                                IsControlAccount =
                                    row.IsControlAccount,

                                IsSystemAccount =
                                    row.IsSystemAccount,

                                DebitMovement =
                                    row.DebitMovement,

                                CreditMovement =
                                    row.CreditMovement,

                                DebitBalance =
                                    row.DebitBalance,

                                CreditBalance =
                                    row.CreditBalance
                            })
                        .ToList(),

                Total =
                    page.Total,

                Page =
                    query.Page,

                PageSize =
                    query.PageSize
            };
        }

        private static string?
            NormalizeOptional(
                string? value)
        {
            if (
                string.IsNullOrWhiteSpace(
                    value)
            )
            {
                return null;
            }

            return value.Trim();
        }
    }
}