using System.ComponentModel.DataAnnotations;
using BankingService.API.DTOs;
using BankingService.API.Models;
using BankingService.API.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BankingService.API.Services
{
    public class BankAccountService : IBankAccountService
    {
        private readonly IBankAccountRepository _repository;
        private readonly IBankTransactionRepository _transactions;

        public BankAccountService(
            IBankAccountRepository repository,
            IBankTransactionRepository transactions)
        {
            _repository = repository;
            _transactions = transactions;
        }

        public async Task<BankAccountListDto> GetPagedAsync(
            BankAccountQueryDto query,
            CancellationToken cancellationToken)
        {
            Validate(query);

            var source = _repository.Query();

            if (query.CompanyId.HasValue)
            {
                source = source.Where(
                    account => account.CompanyId == query.CompanyId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Currency))
            {
                var currency = query.Currency.Trim().ToUpperInvariant();

                source = source.Where(
                    account => account.Currency == currency);
            }

            if (query.IsActive.HasValue)
            {
                source = source.Where(
                    account => account.IsActive == query.IsActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();

                source = source.Where(
                    account =>
                        account.Name.Contains(search) ||
                        account.BankName.Contains(search) ||
                        account.LastFour.Contains(search));
            }

            var total = await source.CountAsync(
                cancellationToken);

            var lastPage = Math.Max(
                1,
                (int)Math.Ceiling(total / (double)query.PageSize));

            var page = Math.Min(
                query.Page,
                lastPage);

            var accounts = await source
                .OrderBy(account => account.Name)
                .ThenBy(account => account.Id)
                .Skip((page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);

            var companies = await _repository.GetCompaniesAsync(
                cancellationToken);

            var companyNames = companies.ToDictionary(
                company => company.Id,
                company => company.Name);

            var accountingAccounts =
                await _repository.GetAccountingAccountsAsync(
                    accounts
                        .Where(account =>
                            account.AccountingAccountId.HasValue)
                        .Select(account =>
                            account.AccountingAccountId!.Value),
                    cancellationToken);

            var accountingAccountsById =
                accountingAccounts.ToDictionary(
                    account => account.Id);

            var data = accounts.Select(account =>
            {
                companyNames.TryGetValue(
                    account.CompanyId,
                    out var companyName);

                BankingAccountingAccountLookup?
                    accountingAccount = null;

                if (account.AccountingAccountId.HasValue)
                {
                    accountingAccountsById.TryGetValue(
                        account.AccountingAccountId.Value,
                        out accountingAccount);
                }

                return ToResponse(
                    account,
                    companyName ?? "",
                    accountingAccount);
            }).ToList();

            return new BankAccountListDto
            {
                Data = data,
                Total = total,
                Page = page,
                PageSize = query.PageSize,
            };
        }

        public async Task<BankAccountResponseDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            var account = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            if (account == null)
            {
                return null;
            }

            var company = await _repository.GetCompanyAsync(
                account.CompanyId,
                cancellationToken);

            var accountingAccount =
                account.AccountingAccountId.HasValue
                    ? await _repository.GetAccountingAccountAsync(
                        account.AccountingAccountId.Value,
                        account.CompanyId,
                        cancellationToken)
                    : null;

            return ToResponse(
                account,
                company?.Name ?? "",
                accountingAccount);
        }

        public async Task<BankAccountResponseDto> CreateAsync(
            CreateBankAccountDto dto,
            CancellationToken cancellationToken)
        {
            Validate(dto);

            var name = dto.Name.Trim();
            var bankName = dto.BankName.Trim();
            var accountNumber = dto.AccountNumber.Trim();
            var currencyCode = dto.Currency.Trim().ToUpperInvariant();

            var company = await _repository.GetCompanyAsync(
                dto.CompanyId,
                cancellationToken);

            if (company == null)
            {
                throw new ValidationException(
                    "Select an existing company.");
            }

            var accountingAccount =
                await ValidateAccountingAccountAsync(
                    dto.AccountingAccountId,
                    dto.CompanyId,
                    cancellationToken);

            var currency = await _repository.GetCurrencyAsync(
                currencyCode,
                cancellationToken);

            if (currency == null || !currency.IsActive)
            {
                throw new ValidationException(
                    "Select an active currency from Currency Master.");
            }

            if (
                currency.DecimalPlaces < 0 ||
                currency.DecimalPlaces > 4)
            {
                throw new ValidationException(
                    "This currency has unsupported decimal precision.");
            }

            ValidateOpeningBalance(
                dto.OpeningBalance,
                currency.DecimalPlaces);

            var exists = await _repository.Query()
                .AnyAsync(
                    account =>
                        account.CompanyId == dto.CompanyId &&
                        account.BankName == bankName &&
                        account.AccountNumber == accountNumber &&
                        account.Currency == currencyCode,
                    cancellationToken);

            if (exists)
            {
                throw new ValidationException(
                    "This bank account already exists for the company and currency.");
            }

            var ledgerAlreadyLinked = await _repository.Query()
                .AnyAsync(
                    account =>
                        account.CompanyId == dto.CompanyId &&
                        account.AccountingAccountId ==
                            dto.AccountingAccountId,
                    cancellationToken);

            if (ledgerAlreadyLinked)
            {
                throw new ValidationException(
                    "This accounting ledger is already linked to another bank account.");
            }

            var account = new BankAccount
            {
                CompanyId = company.Id,
                AccountingAccountId =
                    accountingAccount.Id,
                Name = name,
                BankName = bankName,
                AccountNumber = accountNumber,
                LastFour = accountNumber.Length >= 4
                    ? accountNumber[^4..]
                    : accountNumber,
                Currency = currency.CurrencyCode,
                CurrencyDecimalPlaces = currency.DecimalPlaces,
                OpeningBalance = dto.OpeningBalance,
                OpeningBalanceDate = dto.OpeningBalanceDate,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
            };

            await _repository.AddAsync(
                account,
                cancellationToken);

            await _repository.SaveChangesAsync(
                cancellationToken);

            return ToResponse(
                account,
                company.Name,
                accountingAccount);
        }

        public async Task<BankAccountResponseDto?> UpdateAsync(
            int id,
            UpdateBankAccountDto dto,
            CancellationToken cancellationToken)
        {
            Validate(dto);

            var account = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            if (account == null)
            {
                return null;
            }

            if (!account.RowVersion.SequenceEqual(dto.RowVersion))
            {
                throw new DbUpdateConcurrencyException(
                    "This bank account has changed. Reload it before saving.");
            }

            var accountingAccount =
                await ValidateAccountingAccountAsync(
                    dto.AccountingAccountId,
                    account.CompanyId,
                    cancellationToken);

            var ledgerAlreadyLinked = await _repository.Query()
                .AnyAsync(
                    row =>
                        row.Id != account.Id &&
                        row.CompanyId == account.CompanyId &&
                        row.AccountingAccountId ==
                            dto.AccountingAccountId,
                    cancellationToken);

            if (ledgerAlreadyLinked)
            {
                throw new ValidationException(
                    "This accounting ledger is already linked to another bank account.");
            }

            account.AccountingAccountId =
                accountingAccount.Id;
            account.Name = dto.Name.Trim();
            account.BankName = dto.BankName.Trim();
            account.IsActive = dto.IsActive;
            account.ModifiedDate = DateTime.UtcNow;

            await _repository.SaveChangesAsync(
                cancellationToken);

            var company = await _repository.GetCompanyAsync(
                account.CompanyId,
                cancellationToken);

            return ToResponse(
                account,
                company?.Name ?? "",
                accountingAccount);
        }

        public async Task<BankAccountResponseDto?> ConfigureOpeningBalanceAsync(
            int id,
            ConfigureOpeningBalanceDto dto,
            CancellationToken cancellationToken)
        {
            Validate(dto);

            var account = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            if (account == null)
            {
                return null;
            }

            if (!account.RowVersion.SequenceEqual(dto.RowVersion))
            {
                throw new DbUpdateConcurrencyException(
                    "This bank account has changed. Reload it before saving.");
            }

            if (account.OpeningBalanceDate.HasValue)
            {
                throw new ValidationException(
                    "Opening balance is already configured for this account.");
            }

            if (
                account.CurrencyDecimalPlaces < 0 ||
                account.CurrencyDecimalPlaces > 4)
            {
                throw new ValidationException(
                    "The account has unsupported currency precision.");
            }

            ValidateOpeningBalance(
                dto.OpeningBalance,
                account.CurrencyDecimalPlaces);

            account.OpeningBalance = dto.OpeningBalance;
            account.OpeningBalanceDate = dto.OpeningBalanceDate;
            account.ModifiedDate = DateTime.UtcNow;

            await _repository.SaveChangesAsync(
                cancellationToken);

            var company = await _repository.GetCompanyAsync(
                account.CompanyId,
                cancellationToken);

            var accountingAccount =
                account.AccountingAccountId.HasValue
                    ? await _repository.GetAccountingAccountAsync(
                        account.AccountingAccountId.Value,
                        account.CompanyId,
                        cancellationToken)
                    : null;

            return ToResponse(
                account,
                company?.Name ?? "",
                accountingAccount);
        }

        public async Task<BankAccountBalanceDto?> GetBalanceAsync(
            int id,
            CancellationToken cancellationToken)
        {
            var account = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            if (account == null)
            {
                return null;
            }

            var company = await _repository.GetCompanyAsync(
                account.CompanyId,
                cancellationToken);

            var result = new BankAccountBalanceDto
            {
                BankAccountId = account.Id,
                BankAccountName = account.Name,
                CompanyId = account.CompanyId,
                CompanyName = company?.Name ?? "",
                Currency = account.Currency,
                CurrencyDecimalPlaces = account.CurrencyDecimalPlaces,
                OpeningBalanceDate = account.OpeningBalanceDate,
                IsOpeningBalanceConfigured = account.OpeningBalanceDate.HasValue,
            };

            if (!account.OpeningBalanceDate.HasValue)
            {
                return result;
            }

            var openingDate = account.OpeningBalanceDate.Value;

            var totals = await _transactions.Query()
                .Where(transaction =>
                    transaction.BankAccountId == account.Id &&
                    transaction.CompanyId == account.CompanyId &&
                    transaction.Status == "Posted" &&
                    transaction.TransactionDate >= openingDate)
                .GroupBy(transaction => 1)
                .Select(group => new
                {
                    MoneyIn = group.Sum(transaction =>
                        transaction.Direction == BankTransactionDirection.MoneyIn
                            ? transaction.Amount
                            : 0m),

                    MoneyOut = group.Sum(transaction =>
                        transaction.Direction == BankTransactionDirection.MoneyOut
                            ? transaction.Amount
                            : 0m),

                    InvalidCurrencyCount = group.Count(transaction =>
                        transaction.Currency != account.Currency ||
                        transaction.CurrencyDecimalPlaces != account.CurrencyDecimalPlaces)
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (totals != null && totals.InvalidCurrencyCount > 0)
            {
                throw new ValidationException(
                    "Posted transactions contain currency details that do not match this account.");
            }

            var moneyIn = totals?.MoneyIn ?? 0m;
            var moneyOut = totals?.MoneyOut ?? 0m;

            result.OpeningBalance = account.OpeningBalance;
            result.PostedMoneyIn = moneyIn;
            result.PostedMoneyOut = moneyOut;

            result.CurrentBalance =
                account.OpeningBalance +
                moneyIn -
                moneyOut;

            return result;
        }

        public async Task<List<BankingCompanyLookup>> GetCompaniesAsync(
            CancellationToken cancellationToken)
        {
            return await _repository.GetCompaniesAsync(
                cancellationToken);
        }

        public async Task<List<BankingCurrencyLookup>> GetCurrenciesAsync(
            CancellationToken cancellationToken)
        {
            return await _repository.GetCurrenciesAsync(
                cancellationToken);
        }

        private static void Validate(
            object value)
        {
            Validator.ValidateObject(
                value,
                new ValidationContext(value),
                validateAllProperties: true);
        }

        private static void ValidateOpeningBalance(
            decimal amount,
            int decimalPlaces)
        {
            const decimal maximum = 99999999999999.9999m;

            if (amount < -maximum || amount > maximum)
            {
                throw new ValidationException(
                    "Opening balance exceeds the supported amount.");
            }

            if (decimal.Round(amount, decimalPlaces) != amount)
            {
                throw new ValidationException(
                    $"Opening balance supports up to {decimalPlaces} decimal places for this currency.");
            }
        }

        private async Task<BankingAccountingAccountLookup>
            ValidateAccountingAccountAsync(
                int accountingAccountId,
                int companyId,
                CancellationToken cancellationToken)
        {
            var accountingAccount =
                await _repository.GetAccountingAccountAsync(
                    accountingAccountId,
                    companyId,
                    cancellationToken);

            if (accountingAccount == null)
            {
                throw new ValidationException(
                    "Select an accounting ledger from the same company.");
            }

            if (
                !accountingAccount.IsActive ||
                !accountingAccount.IsPostingAccount ||
                accountingAccount.IsControlAccount ||
                accountingAccount.IsSystemAccount ||
                !string.Equals(
                    accountingAccount.Type,
                    "Asset",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException(
                    "Select an active non-control Asset posting account.");
            }

            if (!accountingAccount.ParentAccountId.HasValue)
            {
                throw new ValidationException(
                    "Select a posting account under the Bank Accounts group.");
            }

            var parentAccount =
                await _repository.GetAccountingAccountAsync(
                    accountingAccount.ParentAccountId.Value,
                    companyId,
                    cancellationToken);

            if (
                parentAccount == null ||
                !string.Equals(
                    parentAccount.SystemKey,
                    "BANK_ACCOUNTS",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException(
                    "Select a posting account under the Bank Accounts group.");
            }

            return accountingAccount;
        }

        private static BankAccountResponseDto ToResponse(
            BankAccount account,
            string companyName,
            BankingAccountingAccountLookup? accountingAccount)
        {
            return new BankAccountResponseDto
            {
                Id = account.Id,
                CompanyId = account.CompanyId,
                CompanyName = companyName,
                AccountingAccountId =
                    account.AccountingAccountId,
                AccountingAccountCode =
                    accountingAccount?.Code ?? "",
                AccountingAccountName =
                    accountingAccount?.Name ?? "",
                Name = account.Name,
                BankName = account.BankName,
                LastFour = account.LastFour,
                Currency = account.Currency,
                CurrencyDecimalPlaces = account.CurrencyDecimalPlaces,
                OpeningBalance = account.OpeningBalance,
                OpeningBalanceDate = account.OpeningBalanceDate,
                IsActive = account.IsActive,
                CreatedDate = account.CreatedDate,
                ModifiedDate = account.ModifiedDate,
                RowVersion = account.RowVersion,
            };
        }
    }
}
