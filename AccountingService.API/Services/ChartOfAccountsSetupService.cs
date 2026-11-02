using System.ComponentModel.DataAnnotations;
using AccountingService.API.DTOs;
using AccountingService.API.Models;
using AccountingService.API.Repositories;

namespace AccountingService.API.Services
{
    public class ChartOfAccountsSetupService
        : IChartOfAccountsSetupService
    {
        private readonly IAccountRepository _repository;

        public ChartOfAccountsSetupService(
            IAccountRepository repository)
        {
            _repository = repository;
        }

        public async Task<IReadOnlyList<AccountResponseDto>>
            SetupAsync(
                int companyId,
                CancellationToken cancellationToken)
        {
            if (companyId < 1)
            {
                throw new ValidationException(
                    "Select an existing company.");
            }

            var companies =
                await _repository.GetCompaniesAsync(
                    cancellationToken);

            var company = companies.FirstOrDefault(
                value => value.Id == companyId);

            if (company == null)
            {
                throw new ValidationException(
                    "Select an existing company.");
            }

            var templates = GetTemplates(
                company.Country);

            var existingAccounts =
                (await _repository.GetTreeAsync(
                    companyId,
                    cancellationToken))
                .ToList();

            var accountsByCode =
                existingAccounts.ToDictionary(
                    account => account.Code,
                    StringComparer.OrdinalIgnoreCase);

            var accountsBySystemKey =
                existingAccounts
                    .Where(account =>
                        !string.IsNullOrWhiteSpace(
                            account.SystemKey))
                    .ToDictionary(
                        account => account.SystemKey!,
                        StringComparer.OrdinalIgnoreCase);

            var resolvedAccounts =
                new Dictionary<string, Account>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (var template in templates)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                Account? accountByCode = null;
                Account? accountBySystemKey = null;

                accountsByCode.TryGetValue(
                    template.Code,
                    out accountByCode);

                if (!string.IsNullOrWhiteSpace(
                    template.SystemKey))
                {
                    accountsBySystemKey.TryGetValue(
                        template.SystemKey,
                        out accountBySystemKey);
                }

                if (accountByCode != null &&
                    accountBySystemKey != null &&
                    accountByCode.Id !=
                    accountBySystemKey.Id)
                {
                    throw new ValidationException(
                        $"Account code {template.Code} and " +
                        $"system key {template.SystemKey} " +
                        "belong to different accounts.");
                }

                var account =
                    accountBySystemKey ??
                    accountByCode;

                int? parentAccountId = null;

                if (!string.IsNullOrWhiteSpace(
                    template.ParentCode))
                {
                    if (!resolvedAccounts.TryGetValue(
                        template.ParentCode,
                        out var parentAccount))
                    {
                        throw new InvalidOperationException(
                            $"Parent account {template.ParentCode} " +
                            $"must be created before " +
                            $"{template.Code}.");
                    }

                    parentAccountId =
                        parentAccount.Id;
                }

                if (account == null)
                {
                    account = new Account
                    {
                        CompanyId = companyId,
                        Code = template.Code,
                        Name = template.Name,
                        Type = template.Type,

                        NormalBalance =
                            template.NormalBalance,

                        ParentAccountId =
                            parentAccountId,

                        IsPostingAccount =
                            template.IsPostingAccount,

                        IsControlAccount =
                            template.IsControlAccount,

                        IsSystemAccount =
                            !string.IsNullOrWhiteSpace(
                                template.SystemKey),

                        SystemKey =
                            template.SystemKey,

                        IsActive = true,

                        Description =
                            template.Description,

                        CreatedDate =
                            DateTime.UtcNow
                    };

                    await _repository.AddAsync(
                        account,
                        cancellationToken);

                    await _repository.SaveChangesAsync(
                        cancellationToken);

                    existingAccounts.Add(account);

                    accountsByCode[account.Code] =
                        account;

                    if (!string.IsNullOrWhiteSpace(
                        account.SystemKey))
                    {
                        accountsBySystemKey[
                            account.SystemKey] =
                            account;
                    }
                }
                else
                {
                    ValidateExistingAccount(
                        account,
                        template,
                        parentAccountId);

                    var trackedAccount =
                        await _repository.GetByIdAsync(
                            account.Id,
                            companyId,
                            cancellationToken);

                    if (trackedAccount == null)
                    {
                        throw new InvalidOperationException(
                            "An existing account could not " +
                            "be loaded.");
                    }

                    var wasChanged = false;

                    if (!string.IsNullOrWhiteSpace(
                        template.SystemKey) &&
                        string.IsNullOrWhiteSpace(
                            trackedAccount.SystemKey))
                    {
                        trackedAccount.SystemKey =
                            template.SystemKey;

                        trackedAccount.IsSystemAccount =
                            true;

                        wasChanged = true;
                    }

                    if (wasChanged)
                    {
                        trackedAccount.ModifiedDate =
                            DateTime.UtcNow;

                        await _repository.SaveChangesAsync(
                            cancellationToken);
                    }

                    account = trackedAccount;
                }

                resolvedAccounts[template.Code] =
                    account;
            }

            var savedAccounts =
                await _repository.GetTreeAsync(
                    companyId,
                    cancellationToken);

            var templateCodes =
                templates
                    .Select(template => template.Code)
                    .ToHashSet(
                        StringComparer.OrdinalIgnoreCase);

            var templateSystemKeys =
                templates
                    .Where(template =>
                        !string.IsNullOrWhiteSpace(
                            template.SystemKey))
                    .Select(template =>
                        template.SystemKey!)
                    .ToHashSet(
                        StringComparer.OrdinalIgnoreCase);

            return savedAccounts
                .Where(account =>
                    templateCodes.Contains(
                        account.Code) ||
                    (
                        account.SystemKey != null &&
                        templateSystemKeys.Contains(
                            account.SystemKey)
                    ))
                .OrderBy(account => account.Code)
                .Select(ToResponse)
                .ToList();
        }

        private static void ValidateExistingAccount(
            Account account,
            AccountTemplate template,
            int? parentAccountId)
        {
            if (account.Type != template.Type)
            {
                throw new ValidationException(
                    $"Account {account.Code} has an " +
                    "incorrect account type.");
            }

            if (account.NormalBalance !=
                template.NormalBalance)
            {
                throw new ValidationException(
                    $"Account {account.Code} has an " +
                    "incorrect normal balance.");
            }

            if (account.IsPostingAccount !=
                template.IsPostingAccount)
            {
                throw new ValidationException(
                    $"Account {account.Code} has an " +
                    "incorrect posting-account setting.");
            }

            if (account.ParentAccountId !=
                parentAccountId)
            {
                throw new ValidationException(
                    $"Account {account.Code} has an " +
                    "incorrect parent account.");
            }

            if (!string.IsNullOrWhiteSpace(
                account.SystemKey) &&
                !string.Equals(
                    account.SystemKey,
                    template.SystemKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException(
                    $"Account {account.Code} already has " +
                    "a different system key.");
            }
        }

        private static IReadOnlyList<AccountTemplate>
            GetTemplates(
                string country)
        {
            var templates =
                GetCommonTemplates()
                    .ToList();

            switch (country.Trim().ToUpperInvariant())
            {
                case "AE":
                    templates.AddRange(
                        GetAeTaxTemplates());

                    break;

                case "IN":
                    templates.AddRange(
                        GetIndiaTaxTemplates());

                    break;

                default:
                    throw new ValidationException(
                        "A standard Chart of Accounts " +
                        "template is not available for " +
                        $"country {country}.");
            }

            return templates
                .OrderBy(template => template.Code)
                .ToList();
        }

        private static IEnumerable<AccountTemplate>
            GetCommonTemplates()
        {
            return
            [
                new(
                    "1000",
                    "Assets",
                    AccountType.Asset,
                    AccountNormalBalance.Debit,
                    null,
                    false,
                    false,
                    "ASSETS",
                    "Main asset account group"),

                new(
                    "1100",
                    "Current Assets",
                    AccountType.Asset,
                    AccountNormalBalance.Debit,
                    "1000",
                    false,
                    false,
                    "CURRENT_ASSETS",
                    "Assets expected to be used within one year"),

                new(
                    "1110",
                    "Cash on Hand",
                    AccountType.Asset,
                    AccountNormalBalance.Debit,
                    "1100",
                    true,
                    false,
                    "CASH_ON_HAND",
                    "Cash and petty cash held by the company"),

                new(
                    "1120",
                    "Bank Accounts",
                    AccountType.Asset,
                    AccountNormalBalance.Debit,
                    "1100",
                    false,
                    false,
                    "BANK_ACCOUNTS",
                    "Parent group for company bank accounts"),

                new(
                    "1130",
                    "Accounts Receivable",
                    AccountType.Asset,
                    AccountNormalBalance.Debit,
                    "1100",
                    true,
                    true,
                    "ACCOUNTS_RECEIVABLE",
                    "Amounts receivable from customers"),

                new(
                    "1140",
                    "Inventory",
                    AccountType.Asset,
                    AccountNormalBalance.Debit,
                    "1100",
                    true,
                    true,
                    "INVENTORY",
                    "Goods held for sale"),

                new(
                    "1150",
                    "Employee Advances",
                    AccountType.Asset,
                    AccountNormalBalance.Debit,
                    "1100",
                    true,
                    false,
                    null,
                    "Advances recoverable from employees"),

                new(
                    "1160",
                    "Prepaid Expenses",
                    AccountType.Asset,
                    AccountNormalBalance.Debit,
                    "1100",
                    true,
                    false,
                    null,
                    "Expenses paid before their accounting period"),

                new(
                    "1170",
                    "Tax Recoverable",
                    AccountType.Asset,
                    AccountNormalBalance.Debit,
                    "1100",
                    false,
                    false,
                    "TAX_RECOVERABLE",
                    "Parent group for recoverable taxes"),

                new(
                    "1200",
                    "Fixed Assets",
                    AccountType.Asset,
                    AccountNormalBalance.Debit,
                    "1000",
                    false,
                    false,
                    "FIXED_ASSETS",
                    "Long-term assets used by the business"),

                new(
                    "1210",
                    "Property, Plant and Equipment",
                    AccountType.Asset,
                    AccountNormalBalance.Debit,
                    "1200",
                    true,
                    false,
                    null,
                    "Property, equipment and other fixed assets"),

                new(
                    "1290",
                    "Accumulated Depreciation",
                    AccountType.Asset,
                    AccountNormalBalance.Credit,
                    "1200",
                    true,
                    false,
                    "ACCUMULATED_DEPRECIATION",
                    "Accumulated depreciation on fixed assets"),

                new(
                    "2000",
                    "Liabilities",
                    AccountType.Liability,
                    AccountNormalBalance.Credit,
                    null,
                    false,
                    false,
                    "LIABILITIES",
                    "Main liability account group"),

                new(
                    "2100",
                    "Current Liabilities",
                    AccountType.Liability,
                    AccountNormalBalance.Credit,
                    "2000",
                    false,
                    false,
                    "CURRENT_LIABILITIES",
                    "Liabilities payable within one year"),

                new(
                    "2110",
                    "Accounts Payable",
                    AccountType.Liability,
                    AccountNormalBalance.Credit,
                    "2100",
                    true,
                    true,
                    "ACCOUNTS_PAYABLE",
                    "Amounts payable to suppliers"),

                new(
                    "2120",
                    "Tax Payables",
                    AccountType.Liability,
                    AccountNormalBalance.Credit,
                    "2100",
                    false,
                    false,
                    "TAX_PAYABLES",
                    "Parent group for taxes payable"),

                new(
                    "2130",
                    "Accrued Expenses",
                    AccountType.Liability,
                    AccountNormalBalance.Credit,
                    "2100",
                    true,
                    false,
                    null,
                    "Expenses incurred but not yet paid"),

                new(
                    "2140",
                    "Employee Payables",
                    AccountType.Liability,
                    AccountNormalBalance.Credit,
                    "2100",
                    true,
                    false,
                    null,
                    "Amounts payable to employees"),

                new(
                    "2200",
                    "Long-term Liabilities",
                    AccountType.Liability,
                    AccountNormalBalance.Credit,
                    "2000",
                    false,
                    false,
                    null,
                    "Liabilities payable after one year"),

                new(
                    "2210",
                    "Loans Payable",
                    AccountType.Liability,
                    AccountNormalBalance.Credit,
                    "2200",
                    true,
                    false,
                    null,
                    "Long-term loans payable"),

                new(
                    "3000",
                    "Equity",
                    AccountType.Equity,
                    AccountNormalBalance.Credit,
                    null,
                    false,
                    false,
                    "EQUITY",
                    "Main owners' equity account group"),

                new(
                    "3100",
                    "Share Capital",
                    AccountType.Equity,
                    AccountNormalBalance.Credit,
                    "3000",
                    true,
                    false,
                    "SHARE_CAPITAL",
                    "Capital contributed by shareholders"),

                new(
                    "3200",
                    "Retained Earnings",
                    AccountType.Equity,
                    AccountNormalBalance.Credit,
                    "3000",
                    true,
                    false,
                    "RETAINED_EARNINGS",
                    "Accumulated retained profits and losses"),

                new(
                    "3300",
                    "Current Year Earnings",
                    AccountType.Equity,
                    AccountNormalBalance.Credit,
                    "3000",
                    true,
                    false,
                    "CURRENT_YEAR_EARNINGS",
                    "Profit or loss for the current financial year"),

                new(
                    "4000",
                    "Income",
                    AccountType.Income,
                    AccountNormalBalance.Credit,
                    null,
                    false,
                    false,
                    "INCOME",
                    "Main income account group"),

                new(
                    "4100",
                    "Sales Revenue",
                    AccountType.Income,
                    AccountNormalBalance.Credit,
                    "4000",
                    true,
                    false,
                    "SALES_REVENUE",
                    "Revenue from normal business sales"),

                new(
                    "4200",
                    "Other Income",
                    AccountType.Income,
                    AccountNormalBalance.Credit,
                    "4000",
                    true,
                    false,
                    null,
                    "Income outside normal business sales"),

                new(
                    "5000",
                    "Expenses",
                    AccountType.Expense,
                    AccountNormalBalance.Debit,
                    null,
                    false,
                    false,
                    "EXPENSES",
                    "Main expense account group"),

                new(
                    "5100",
                    "Cost of Goods Sold",
                    AccountType.Expense,
                    AccountNormalBalance.Debit,
                    "5000",
                    true,
                    false,
                    "COST_OF_GOODS_SOLD",
                    "Direct cost of goods or services sold"),

                new(
                    "5200",
                    "Operating Expenses",
                    AccountType.Expense,
                    AccountNormalBalance.Debit,
                    "5000",
                    false,
                    false,
                    null,
                    "Parent group for operating expenses"),

                new(
                    "5210",
                    "Salaries and Wages",
                    AccountType.Expense,
                    AccountNormalBalance.Debit,
                    "5200",
                    true,
                    false,
                    null,
                    "Employee salary and wage expenses"),

                new(
                    "5220",
                    "Rent Expense",
                    AccountType.Expense,
                    AccountNormalBalance.Debit,
                    "5200",
                    true,
                    false,
                    null,
                    "Office and property rent expenses"),

                new(
                    "5230",
                    "Utilities Expense",
                    AccountType.Expense,
                    AccountNormalBalance.Debit,
                    "5200",
                    true,
                    false,
                    null,
                    "Electricity, water and utility expenses"),

                new(
                    "5240",
                    "Depreciation Expense",
                    AccountType.Expense,
                    AccountNormalBalance.Debit,
                    "5200",
                    true,
                    false,
                    "DEPRECIATION_EXPENSE",
                    "Periodic depreciation expense"),

                new(
                    "5250",
                    "Bank Charges",
                    AccountType.Expense,
                    AccountNormalBalance.Debit,
                    "5200",
                    true,
                    false,
                    null,
                    "Bank fees and transaction charges")
            ];
        }

        private static IEnumerable<AccountTemplate>
            GetAeTaxTemplates()
        {
            return
            [
                new(
                    "1171",
                    "Input VAT Recoverable",
                    AccountType.Asset,
                    AccountNormalBalance.Debit,
                    "1170",
                    true,
                    true,
                    "INPUT_VAT",
                    "UAE input VAT recoverable"),

                new(
                    "2121",
                    "Output VAT Payable",
                    AccountType.Liability,
                    AccountNormalBalance.Credit,
                    "2120",
                    true,
                    true,
                    "OUTPUT_VAT",
                    "UAE output VAT payable")
            ];
        }

        private static IEnumerable<AccountTemplate>
            GetIndiaTaxTemplates()
        {
            return
            [
                new(
                    "1171",
                    "Input CGST",
                    AccountType.Asset,
                    AccountNormalBalance.Debit,
                    "1170",
                    true,
                    true,
                    "INPUT_CGST",
                    "India input CGST credit"),

                new(
                    "1172",
                    "Input SGST",
                    AccountType.Asset,
                    AccountNormalBalance.Debit,
                    "1170",
                    true,
                    true,
                    "INPUT_SGST",
                    "India input SGST credit"),

                new(
                    "1173",
                    "Input IGST",
                    AccountType.Asset,
                    AccountNormalBalance.Debit,
                    "1170",
                    true,
                    true,
                    "INPUT_IGST",
                    "India input IGST credit"),

                new(
                    "1174",
                    "TDS Receivable",
                    AccountType.Asset,
                    AccountNormalBalance.Debit,
                    "1170",
                    true,
                    true,
                    "TDS_RECEIVABLE",
                    "India tax deducted at source receivable"),

                new(
                    "2121",
                    "Output CGST",
                    AccountType.Liability,
                    AccountNormalBalance.Credit,
                    "2120",
                    true,
                    true,
                    "OUTPUT_CGST",
                    "India output CGST payable"),

                new(
                    "2122",
                    "Output SGST",
                    AccountType.Liability,
                    AccountNormalBalance.Credit,
                    "2120",
                    true,
                    true,
                    "OUTPUT_SGST",
                    "India output SGST payable"),

                new(
                    "2123",
                    "Output IGST",
                    AccountType.Liability,
                    AccountNormalBalance.Credit,
                    "2120",
                    true,
                    true,
                    "OUTPUT_IGST",
                    "India output IGST payable"),

                new(
                    "2124",
                    "TDS Payable",
                    AccountType.Liability,
                    AccountNormalBalance.Credit,
                    "2120",
                    true,
                    true,
                    "TDS_PAYABLE",
                    "India tax deducted at source payable")
            ];
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

        private sealed record AccountTemplate(
            string Code,
            string Name,
            AccountType Type,
            AccountNormalBalance NormalBalance,
            string? ParentCode,
            bool IsPostingAccount,
            bool IsControlAccount,
            string? SystemKey,
            string Description);
    }
}