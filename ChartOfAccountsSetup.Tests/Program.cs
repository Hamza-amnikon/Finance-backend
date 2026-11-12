using System.ComponentModel.DataAnnotations;
using AccountingService.API.DTOs;
using AccountingService.API.Models;
using AccountingService.API.Repositories;
using AccountingService.API.Services;

var repository = new SetupRepository();
var service = new ChartOfAccountsSetupService(repository);

var firstCompany = await service.SetupAsync(73, CancellationToken.None);
var firstIds = firstCompany.Select(account => account.Id).ToArray();
var repeated = await service.SetupAsync(73, CancellationToken.None);
Check(firstIds.SequenceEqual(repeated.Select(account => account.Id)), "Repeating setup reuses the existing accounts.");
Check(repository.Accounts.Count == firstCompany.Count, "Repeated setup creates no duplicate records.");
Check(firstCompany.Any(account => account.SystemKey == "INPUT_VAT"), "The company country determines its VAT accounts.");
Check(firstCompany.Any(account => account.SystemKey == "BANK_ACCOUNTS" && !account.IsPostingAccount), "Setup creates the required bank account group.");

var secondCompany = await service.SetupAsync(204, CancellationToken.None);
Check(secondCompany.All(account => account.CompanyId == 204), "A different company receives its own accounts.");
Check(secondCompany.Any(account => account.SystemKey == "INPUT_CGST") &&
    secondCompany.Any(account => account.SystemKey == "INPUT_SGST"), "The India company receives separate GST component accounts.");
Check(!secondCompany.Any(account => account.SystemKey == "INPUT_VAT"), "Accounts from the first company are not reused for the second company.");

var total = repository.Accounts.Count;
await service.SetupAsync(204, CancellationToken.None);
Check(repository.Accounts.Count == total, "Repeating setup for the second company also creates no duplicates.");

try
{
    await service.SetupAsync(999, CancellationToken.None);
    throw new InvalidOperationException("An unknown company was accepted.");
}
catch (ValidationException)
{
    Check(repository.Accounts.Count == total, "Unknown companies are rejected without creating accounts.");
}

static void Check(bool condition, string description)
{
    if (!condition)
    {
        throw new InvalidOperationException("FAIL: " + description);
    }

    Console.WriteLine("PASS: " + description);
}

internal sealed class SetupRepository : IAccountRepository
{
    public List<Account> Accounts { get; } = [];

    private readonly List<AccountingCompanyLookup> _companies =
    [
        new()
        {
            Id = 73,
            Name = "First test company",
            Country = "AE",
            Currency = "AED"
        },
        new()
        {
            Id = 204,
            Name = "Second test company",
            Country = "IN",
            Currency = "INR"
        }
    ];

    public Task<List<AccountingCompanyLookup>> GetCompaniesAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_companies);
    }

    public Task<IReadOnlyList<Account>> GetTreeAsync(int companyId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Account>>(Accounts
            .Where(account => account.CompanyId == companyId)
            .OrderBy(account => account.Code)
            .ToList());
    }

    public Task<Account?> GetByIdAsync(int id, int companyId, CancellationToken cancellationToken)
    {
        return Task.FromResult(Accounts.SingleOrDefault(account => account.Id == id && account.CompanyId == companyId));
    }

    public Task AddAsync(Account account, CancellationToken cancellationToken)
    {
        account.Id = Accounts.Count + 1;
        account.Company = _companies.Single(company => company.Id == account.CompanyId);
        account.ParentAccount = Accounts.SingleOrDefault(parent => parent.Id == account.ParentAccountId);
        Accounts.Add(account);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<Account> Accounts, int Total)> GetAllAsync(AccountQueryDto query, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<bool> CompanyExistsAsync(int companyId, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<bool> CodeExistsAsync(int companyId, string code, int? excludeAccountId, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<bool> HasChildrenAsync(int accountId, int companyId, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<bool> WouldCreateCycleAsync(int accountId, int parentAccountId, int companyId, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }
}
