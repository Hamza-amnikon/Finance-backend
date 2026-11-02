using System.ComponentModel.DataAnnotations;
using AccountingService.API.DTOs;
using AccountingService.API.Models;
using AccountingService.API.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AccountingService.API.Services
{
    public class AccountService : IAccountService
    {
        private readonly IAccountRepository _repository;

        public AccountService(
            IAccountRepository repository)
        {
            _repository = repository;
        }

        public async Task<AccountListDto> GetPagedAsync(
            AccountQueryDto query,
            CancellationToken cancellationToken)
        {
            Validate(query);

            var companyExists =
                await _repository.CompanyExistsAsync(
                    query.CompanyId,
                    cancellationToken);

            if (!companyExists)
            {
                throw new ValidationException(
                    "Select an existing company.");
            }

            var result =
                await _repository.GetAllAsync(
                    query,
                    cancellationToken);

            return new AccountListDto
            {
                Data = result.Accounts
                    .Select(ToResponse)
                    .ToList(),

                Total = result.Total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public async Task<AccountResponseDto?> GetByIdAsync(
            int id,
            int companyId,
            CancellationToken cancellationToken)
        {
            var account =
                await _repository.GetByIdAsync(
                    id,
                    companyId,
                    cancellationToken);

            return account == null
                ? null
                : ToResponse(account);
        }

        public async Task<AccountResponseDto> CreateAsync(
            CreateAccountDto dto,
            CancellationToken cancellationToken)
        {
            Validate(dto);

            var type =
                ValidateAccountType(dto.Type);

            var normalBalance =
                ValidateNormalBalance(
                    dto.NormalBalance);

            var code =
                NormalizeCode(dto.Code);

            var name =
                NormalizeRequiredText(
                    dto.Name,
                    "Account name is required.");

            var companyExists =
                await _repository.CompanyExistsAsync(
                    dto.CompanyId,
                    cancellationToken);

            if (!companyExists)
            {
                throw new ValidationException(
                    "Select an existing company.");
            }

            var codeExists =
                await _repository.CodeExistsAsync(
                    dto.CompanyId,
                    code,
                    excludeAccountId: null,
                    cancellationToken);

            if (codeExists)
            {
                throw new ValidationException(
                    "This account code already exists for the company.");
            }

            var parentAccount =
                await ValidateParentAsync(
                    dto.ParentAccountId,
                    dto.CompanyId,
                    type,
                    accountId: null,
                    cancellationToken);

            var account = new Account
            {
                CompanyId = dto.CompanyId,
                Code = code,
                Name = name,
                Type = type,
                NormalBalance = normalBalance,

                ParentAccountId =
                    parentAccount?.Id,

                ParentAccount =
                    parentAccount,

                IsPostingAccount =
                    dto.IsPostingAccount,

                IsControlAccount = false,
                IsSystemAccount = false,
                IsActive = true,

                Description =
                    dto.Description?.Trim() ?? string.Empty,

                CreatedDate = DateTime.UtcNow
            };

            await _repository.AddAsync(
                account,
                cancellationToken);

            await _repository.SaveChangesAsync(
                cancellationToken);

            var savedAccount =
                await _repository.GetByIdAsync(
                    account.Id,
                    account.CompanyId,
                    cancellationToken);

            if (savedAccount == null)
            {
                throw new InvalidOperationException(
                    "The account was saved but could not be reloaded.");
            }

            return ToResponse(savedAccount);
        }

        public async Task<AccountResponseDto?> UpdateAsync(
            int id,
            int companyId,
            UpdateAccountDto dto,
            CancellationToken cancellationToken)
        {
            Validate(dto);

            var type =
                ValidateAccountType(dto.Type);

            var normalBalance =
                ValidateNormalBalance(
                    dto.NormalBalance);

            var account =
                await _repository.GetByIdAsync(
                    id,
                    companyId,
                    cancellationToken);

            if (account == null)
            {
                return null;
            }

            if (!account.RowVersion.SequenceEqual(
                    dto.RowVersion))
            {
                throw new DbUpdateConcurrencyException(
                    "This account has changed. Reload it before saving.");
            }

            if (account.IsSystemAccount)
            {
                throw new ValidationException(
                    "System accounts cannot be edited.");
            }

            if (account.IsControlAccount)
            {
                throw new ValidationException(
                    "Control accounts are managed by the system.");
            }

            var code =
                NormalizeCode(dto.Code);

            var name =
                NormalizeRequiredText(
                    dto.Name,
                    "Account name is required.");

            var codeExists =
                await _repository.CodeExistsAsync(
                    companyId,
                    code,
                    excludeAccountId: id,
                    cancellationToken);

            if (codeExists)
            {
                throw new ValidationException(
                    "This account code already exists for the company.");
            }

            var hasChildren =
                await _repository.HasChildrenAsync(
                    id,
                    companyId,
                    cancellationToken);

            if (
                account.Type != type &&
                hasChildren)
            {
                throw new ValidationException(
                    "The account type cannot be changed while it has child accounts.");
            }

            if (
                dto.IsPostingAccount &&
                hasChildren)
            {
                throw new ValidationException(
                    "An account with child accounts cannot be a posting account.");
            }

            var parentAccount =
                await ValidateParentAsync(
                    dto.ParentAccountId,
                    companyId,
                    type,
                    id,
                    cancellationToken);

            account.Code = code;
            account.Name = name;
            account.Type = type;
            account.NormalBalance = normalBalance;

            account.ParentAccountId =
                parentAccount?.Id;

            account.ParentAccount =
                parentAccount;

            account.IsPostingAccount =
                dto.IsPostingAccount;

            account.IsActive =
                dto.IsActive;

            account.Description =
                dto.Description?.Trim() ?? string.Empty;

            account.ModifiedDate =
                DateTime.UtcNow;

            await _repository.SaveChangesAsync(
                cancellationToken);

            return ToResponse(account);
        }

        public async Task<IReadOnlyList<AccountResponseDto>>
            GetTreeAsync(
                int companyId,
                CancellationToken cancellationToken)
        {
            if (companyId < 1)
            {
                throw new ValidationException(
                    "Select an existing company.");
            }

            var companyExists =
                await _repository.CompanyExistsAsync(
                    companyId,
                    cancellationToken);

            if (!companyExists)
            {
                throw new ValidationException(
                    "Select an existing company.");
            }

            var accounts =
                await _repository.GetTreeAsync(
                    companyId,
                    cancellationToken);

            return accounts
                .Select(ToResponse)
                .ToList();
        }

        public Task<List<AccountingCompanyLookup>>
            GetCompaniesAsync(
                CancellationToken cancellationToken)
        {
            return _repository.GetCompaniesAsync(
                cancellationToken);
        }

        private async Task<Account?>
            ValidateParentAsync(
                int? parentAccountId,
                int companyId,
                AccountType accountType,
                int? accountId,
                CancellationToken cancellationToken)
        {
            if (!parentAccountId.HasValue)
            {
                return null;
            }

            if (
                accountId.HasValue &&
                parentAccountId.Value ==
                accountId.Value)
            {
                throw new ValidationException(
                    "An account cannot be its own parent.");
            }

            var parentAccount =
                await _repository.GetByIdAsync(
                    parentAccountId.Value,
                    companyId,
                    cancellationToken);

            if (parentAccount == null)
            {
                throw new ValidationException(
                    "Select a parent account from the same company.");
            }

            if (!parentAccount.IsActive)
            {
                throw new ValidationException(
                    "The parent account must be active.");
            }

            if (parentAccount.IsPostingAccount)
            {
                throw new ValidationException(
                    "A posting account cannot be used as a parent.");
            }

            if (parentAccount.Type != accountType)
            {
                throw new ValidationException(
                    "The parent and child accounts must have the same account type.");
            }

            if (accountId.HasValue)
            {
                var createsCycle =
                    await _repository
                        .WouldCreateCycleAsync(
                            accountId.Value,
                            parentAccount.Id,
                            companyId,
                            cancellationToken);

                if (createsCycle)
                {
                    throw new ValidationException(
                        "This parent selection would create a circular account hierarchy.");
                }
            }

            return parentAccount;
        }

        private static AccountType
            ValidateAccountType(
                AccountType? value)
        {
            if (
                !value.HasValue ||
                !Enum.IsDefined(value.Value))
            {
                throw new ValidationException(
                    "Select a valid account type.");
            }

            return value.Value;
        }

        private static AccountNormalBalance
            ValidateNormalBalance(
                AccountNormalBalance? value)
        {
            if (
                !value.HasValue ||
                !Enum.IsDefined(value.Value))
            {
                throw new ValidationException(
                    "Select a valid normal balance.");
            }

            return value.Value;
        }

        private static string NormalizeCode(
            string code)
        {
            var normalizedCode =
                NormalizeRequiredText(
                    code,
                    "Account code is required.")
                .ToUpperInvariant();

            return normalizedCode;
        }

        private static string NormalizeRequiredText(
            string value,
            string errorMessage)
        {
            var normalizedValue =
                value.Trim();

            if (string.IsNullOrWhiteSpace(
                    normalizedValue))
            {
                throw new ValidationException(
                    errorMessage);
            }

            return normalizedValue;
        }

        private static void Validate(
            object value)
        {
            Validator.ValidateObject(
                value,
                new ValidationContext(value),
                validateAllProperties: true);
        }

        private static AccountResponseDto ToResponse(
            Account account)
        {
            return new AccountResponseDto
            {
                Id = account.Id,
                CompanyId = account.CompanyId,

                CompanyName =
                    account.Company.Name,

                Country =
                    account.Company.Country,

                Currency =
                    account.Company.Currency,

                Code = account.Code,
                Name = account.Name,
                Type = account.Type,

                NormalBalance =
                    account.NormalBalance,

                ParentAccountId =
                    account.ParentAccountId,

                ParentAccountCode =
                    account.ParentAccount?.Code,

                ParentAccountName =
                    account.ParentAccount?.Name,

                IsPostingAccount =
                    account.IsPostingAccount,

                IsControlAccount =
                    account.IsControlAccount,

                IsSystemAccount =
                    account.IsSystemAccount,

                SystemKey =
                    account.SystemKey,

                IsActive =
                    account.IsActive,

                Description =
                    account.Description,

                CreatedDate =
                    account.CreatedDate,

                ModifiedDate =
                    account.ModifiedDate,

                RowVersion =
                    account.RowVersion
            };
        }
    }
}
