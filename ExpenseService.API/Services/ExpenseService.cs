using System.ComponentModel.DataAnnotations;
using ExpenseService.API.DTOs;
using ExpenseService.API.Models;
using ExpenseService.API.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ExpenseService.API.Services
{
    public class ExpenseService : IExpenseService
    {
        public const string LockedMessage =
            "Only drafts can be edited. Void a recorded expense to correct it.";

        private readonly IExpenseRepository _repository;
        private readonly IExpenseLookupService _lookups;
        private readonly IExpenseExchangeRateService _rates;

        public ExpenseService(
            IExpenseRepository repository,
            IExpenseLookupService lookups,
            IExpenseExchangeRateService rates)
        {
            _repository = repository;
            _lookups = lookups;
            _rates = rates;
        }

        public async Task<ExpenseListDto> GetPagedAsync(
            ExpenseQueryDto query,
            CancellationToken cancellationToken)
        {
            Validator.ValidateObject(
                query,
                new ValidationContext(query),
                true);

            if (query.From.HasValue && query.To.HasValue && query.From > query.To)
            {
                throw new ValidationException(
                    "End date must be on or after start date.");
            }

            var display = await _lookups.ResolveCurrencyAsync(
                query.DisplayCurrency,
                false,
                cancellationToken);

            query.DisplayCurrency = display.CurrencyCode;

            var source = _repository.Query(query);
            var total = await source.CountAsync(cancellationToken);

            // Group all matching expenses so totals include every page.
            var groups = await source
                .GroupBy(row => new
                {
                    row.Status,
                    row.Currency,
                    row.Date,
                    row.Rate,
                    row.RateBaseCurrency,
                    row.RateQuoteCurrency,
                    row.RateDate
                })
                .Select(group => new
                {
                    group.Key,
                    Amount = group.Sum(row => row.Amount),
                    Count = group.Count()
                })
                .ToListAsync(cancellationToken);

            var rates = new Dictionary<
                (string Currency, DateOnly Date),
                ExpenseExchangeRateDto?>();

            var totals = new Dictionary<string, decimal>();
            var missing = new Dictionary<string, int>();

            foreach (var group in groups)
            {
                var expense = new ExpenseResponseDto
                {
                    Currency = group.Key.Currency,
                    Date = group.Key.Date,
                    Rate = group.Key.Rate,
                    RateBaseCurrency = group.Key.RateBaseCurrency,
                    RateQuoteCurrency = group.Key.RateQuoteCurrency,
                    RateDate = group.Key.RateDate
                };

                var rate = await ResolveDisplayRateAsync(
                    expense,
                    display.CurrencyCode,
                    rates,
                    cancellationToken);

                var amount = ConvertForDisplay(
                    group.Amount,
                    expense.Currency,
                    display.CurrencyCode,
                    rate);

                if (amount.HasValue)
                {
                    totals[group.Key.Status] =
                        totals.GetValueOrDefault(group.Key.Status) + amount.Value;
                }
                else
                {
                    missing[group.Key.Status] =
                        missing.GetValueOrDefault(group.Key.Status) + group.Count;
                }
            }

            var rows = await source
                .OrderByDescending(row => row.Date)
                .ThenByDescending(row => row.Id)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(row => new ExpenseResponseDto
                {
                    Id = row.Id,
                    RequestId = row.RequestId,
                    CompanyId = row.CompanyId,
                    CompanyName = row.Company.Name,
                    BankAccountId = row.BankAccountId,
                    BankAccountName = row.BankAccount == null
                        ? null
                        : row.BankAccount.Name,
                    Date = row.Date,
                    Supplier = row.Supplier,
                    Category = row.Category,
                    Notes = row.Notes,
                    Amount = row.Amount,
                    Currency = row.Currency,
                    CurrencyDecimalPlaces = row.CurrencyDecimalPlaces,
                    Rate = row.Rate,
                    RateBaseCurrency = row.RateBaseCurrency,
                    RateQuoteCurrency = row.RateQuoteCurrency,
                    RateDate = row.RateDate,
                    Status = row.Status,
                    Reference = row.Reference,
                    VoidReason = row.VoidReason,
                    RowVersion = row.RowVersion,
                    ReceiptName = row.Receipt == null
                        ? null
                        : row.Receipt.FileName
                })
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                var rate = await ResolveDisplayRateAsync(
                    row,
                    display.CurrencyCode,
                    rates,
                    cancellationToken);

                ApplyDisplay(row, display, rate);
            }

            decimal? TotalFor(string status)
            {
                return missing.GetValueOrDefault(status) > 0
                    ? null
                    : ExpenseHelper.Round(
                        totals.GetValueOrDefault(status),
                        display.DecimalPlaces);
            }

            return new ExpenseListDto
            {
                Data = rows,
                Total = total,
                Page = query.Page,
                PageSize = query.PageSize,
                DisplayCurrency = query.DisplayCurrency,
                DisplayCurrencyDecimalPlaces = display.DecimalPlaces,
                MissingRateCount = missing.Values.Sum(),
                Draft = TotalFor("Draft"),
                Recorded = TotalFor("Recorded"),
                Voided = TotalFor("Voided")
            };
        }

        public async Task<ExpenseResponseDto> GetByIdAsync(
            int id,
            string? currency,
            CancellationToken cancellationToken)
        {
            var row = await _repository.GetByIdAsync(
                id,
                cancellationToken)
                ?? throw new KeyNotFoundException("Expense not found.");

            return await ToResponseAsync(
                row,
                currency,
                cancellationToken);
        }

        public async Task<ExpenseResponseDto> SaveAsync(
            int? id,
            ExpenseDto dto,
            CancellationToken cancellationToken)
        {
            ExpenseHelper.Validate(dto);

            if (!id.HasValue)
            {
                var existing = await _repository.GetByRequestIdAsync(
                    dto.RequestId,
                    cancellationToken);

                if (existing != null)
                {
                    if (!SameRequest(existing, dto))
                    {
                        throw new ValidationException(
                            "This request ID was already used for another expense.");
                    }

                    return await ToResponseAsync(
                        existing,
                        dto.Currency,
                        cancellationToken);
                }
            }

            var company = await _repository.GetCompanyAsync(
                dto.CompanyId,
                cancellationToken)
                ?? throw new ValidationException("Select an existing company.");

            var originalCurrency = await _lookups.ResolveCurrencyAsync(
                dto.Currency,
                true,
                cancellationToken);

            await _lookups.ResolveCurrencyAsync(
                dto.RateBaseCurrency,
                true,
                cancellationToken);

            await _lookups.ResolveCurrencyAsync(
                dto.RateQuoteCurrency,
                true,
                cancellationToken);

            if (decimal.Round(dto.Amount, originalCurrency.DecimalPlaces) != dto.Amount)
            {
                throw new ValidationException(
                    "Use the decimal precision configured for the expense currency.");
            }

            BankAccount? account = null;

            if (dto.BankAccountId.HasValue)
            {
                account = await _repository.GetAccountAsync(
                    dto.BankAccountId.Value,
                    cancellationToken)
                    ?? throw new ValidationException("Select an existing bank account.");

                if (account.CompanyId != company.Id)
                {
                    throw new ValidationException(
                        "The account must belong to the selected company.");
                }

                if (account.Currency != dto.Currency)
                {
                    throw new ValidationException(
                        "Expense currency must match the paying account currency.");
                }
            }

            var row = id.HasValue
                ? await _repository.GetByIdAsync(
                    id.Value,
                    cancellationToken)
                    ?? throw new KeyNotFoundException("Expense not found.")
                : new Expense
                {
                    RequestId = dto.RequestId
                };

            if (id.HasValue)
            {
                ExpenseHelper.CheckVersion(
                    row.RowVersion,
                    dto.RowVersion);

                if (row.Status != "Draft")
                {
                    throw new InvalidOperationException(LockedMessage);
                }

                if (row.CompanyId != dto.CompanyId)
                {
                    throw new ValidationException(
                        "An expense cannot be moved to another company.");
                }
            }

            var keepSavedRate =
                id.HasValue &&
                row.Currency == dto.Currency &&
                row.Date == dto.Date &&
                row.RateBaseCurrency == dto.RateBaseCurrency &&
                row.RateQuoteCurrency == dto.RateQuoteCurrency;

            var quote = keepSavedRate
                ? new ExpenseExchangeRateDto
                {
                    Rate = row.Rate,
                    RateDate = row.RateDate,
                    RateBaseCurrency = row.RateBaseCurrency,
                    RateQuoteCurrency = row.RateQuoteCurrency
                }
                : await _rates.GetRateAsync(
                    dto.RateBaseCurrency,
                    dto.RateQuoteCurrency,
                    dto.Date,
                    cancellationToken);

            row.CompanyId = company.Id;
            row.Company = company;
            row.BankAccountId = account?.Id;
            row.BankAccount = account;
            row.Date = dto.Date;
            row.Supplier = dto.Supplier.Trim();
            row.Category = dto.Category.Trim();
            row.Notes = dto.Notes?.Trim() ?? "";
            row.Amount = dto.Amount;
            row.Currency = dto.Currency;
            row.CurrencyDecimalPlaces = originalCurrency.DecimalPlaces;
            row.Rate = quote.Rate;
            row.RateBaseCurrency = quote.RateBaseCurrency;
            row.RateQuoteCurrency = quote.RateQuoteCurrency;
            row.RateDate = quote.RateDate;
            row.Status = dto.Status;
            row.Reference = dto.Reference?.Trim() ?? "";
            row.ModifiedDate = DateTime.UtcNow;

            await _repository.SaveAsync(
                row,
                dto.RowVersion,
                cancellationToken);

            return await ToResponseAsync(
                row,
                dto.Currency,
                cancellationToken);
        }

        public async Task<ExpenseResponseDto> VoidAsync(
            int id,
            VoidExpenseDto dto,
            CancellationToken cancellationToken)
        {
            Validator.ValidateObject(
                dto,
                new ValidationContext(dto),
                true);

            var row = await _repository.GetByIdAsync(
                id,
                cancellationToken)
                ?? throw new KeyNotFoundException("Expense not found.");

            ExpenseHelper.CheckVersion(
                row.RowVersion,
                dto.RowVersion);

            if (row.Status == "Voided")
            {
                throw new InvalidOperationException(LockedMessage);
            }

            row.Status = "Voided";
            row.VoidReason = dto.Reason.Trim();
            row.ModifiedDate = DateTime.UtcNow;

            await _repository.SaveAsync(
                row,
                dto.RowVersion,
                cancellationToken);

            return await ToResponseAsync(
                row,
                row.Currency,
                cancellationToken);
        }

        private static bool SameRequest(
            Expense row,
            ExpenseDto dto)
        {
            return row.CompanyId == dto.CompanyId &&
                row.BankAccountId == dto.BankAccountId &&
                row.Date == dto.Date &&
                row.Supplier == dto.Supplier.Trim() &&
                row.Category == dto.Category.Trim() &&
                row.Notes == (dto.Notes?.Trim() ?? "") &&
                row.Amount == dto.Amount &&
                row.Currency == dto.Currency &&
                row.RateBaseCurrency == dto.RateBaseCurrency &&
                row.RateQuoteCurrency == dto.RateQuoteCurrency &&
                row.Status == dto.Status &&
                row.Reference == (dto.Reference?.Trim() ?? "");
        }

        private async Task<ExpenseResponseDto> ToResponseAsync(
            Expense row,
            string? currency,
            CancellationToken cancellationToken)
        {
            var display = await _lookups.ResolveCurrencyAsync(
                string.IsNullOrWhiteSpace(currency) ? row.Currency : currency,
                false,
                cancellationToken);

            var response = new ExpenseResponseDto
            {
                Id = row.Id,
                RequestId = row.RequestId,
                CompanyId = row.CompanyId,
                CompanyName = row.Company.Name,
                BankAccountId = row.BankAccountId,
                BankAccountName = row.BankAccount?.Name,
                Date = row.Date,
                Supplier = row.Supplier,
                Category = row.Category,
                Notes = row.Notes,
                Amount = row.Amount,
                Currency = row.Currency,
                CurrencyDecimalPlaces = row.CurrencyDecimalPlaces,
                Rate = row.Rate,
                RateBaseCurrency = row.RateBaseCurrency,
                RateQuoteCurrency = row.RateQuoteCurrency,
                RateDate = row.RateDate,
                Status = row.Status,
                Reference = row.Reference,
                VoidReason = row.VoidReason,
                RowVersion = row.RowVersion,
                ReceiptName = row.Receipt?.FileName,
            };

            var rate = await ResolveDisplayRateAsync(
                response,
                display.CurrencyCode,
                new Dictionary<(string Currency, DateOnly Date), ExpenseExchangeRateDto?>(),
                cancellationToken);

            ApplyDisplay(response, display, rate);

            return response;
        }

        private async Task<ExpenseExchangeRateDto?> ResolveDisplayRateAsync(
            ExpenseResponseDto row,
            string displayCurrency,
            Dictionary<(string Currency, DateOnly Date), ExpenseExchangeRateDto?> rates,
            CancellationToken cancellationToken)
        {
            if (row.Currency == displayCurrency)
            {
                return new ExpenseExchangeRateDto
                {
                    RateBaseCurrency = row.Currency,
                    RateQuoteCurrency = row.Currency,
                    Rate = 1m,
                    RateDate = row.Date,
                    Source = "Same currency"
                };
            }

            if (ExpenseHelper.Convert(
                1m,
                row.Currency,
                displayCurrency,
                row.Rate,
                row.RateBaseCurrency,
                row.RateQuoteCurrency).HasValue)
            {
                return new ExpenseExchangeRateDto
                {
                    RateBaseCurrency = row.RateBaseCurrency,
                    RateQuoteCurrency = row.RateQuoteCurrency,
                    Rate = row.Rate,
                    RateDate = row.RateDate,
                    Source = "Saved rate"
                };
            }

            var key = (row.Currency, row.Date);

            if (rates.TryGetValue(key, out var cached))
            {
                return cached;
            }

            ExpenseExchangeRateDto? rate;

            try
            {
                rate = await _rates.GetRateAsync(
                    row.Currency,
                    displayCurrency,
                    row.Date,
                    cancellationToken);
            }
            catch (HttpRequestException)
            {
                rate = null;
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                rate = null;
            }
            catch (ValidationException)
            {
                rate = null;
            }

            rates[key] = rate;

            return rate;
        }

        private static decimal? ConvertForDisplay(
            decimal amount,
            string currency,
            string displayCurrency,
            ExpenseExchangeRateDto? rate)
        {
            return rate == null
                ? null
                : ExpenseHelper.Convert(
                    amount,
                    currency,
                    displayCurrency,
                    rate.Rate,
                    rate.RateBaseCurrency,
                    rate.RateQuoteCurrency);
        }

        private static void ApplyDisplay(
            ExpenseResponseDto row,
            ExpenseCurrencyLookup display,
            ExpenseExchangeRateDto? rate)
        {
            row.DisplayCurrency = display.CurrencyCode;
            row.DisplayCurrencyDecimalPlaces = display.DecimalPlaces;
            row.DisplayAmount = ExpenseHelper.Round(
                ConvertForDisplay(
                    row.Amount,
                    row.Currency,
                    display.CurrencyCode,
                    rate),
                display.DecimalPlaces);

            row.DisplayRate = rate?.Rate;
            row.DisplayRateBaseCurrency = rate?.RateBaseCurrency;
            row.DisplayRateQuoteCurrency = rate?.RateQuoteCurrency;
            row.DisplayRateDate = rate?.RateDate;
            row.DisplayRateSource = rate?.Source;
        }
    }
}
