using AccountingService.API.Data;
using AccountingService.API.DTOs;
using AccountingService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingService.API.Repositories
{
    public class AccountRepository : IAccountRepository
    {
        private readonly AccountingDbContext _context;

        public AccountRepository(
            AccountingDbContext context)
        {
            _context = context;
        }

        public async Task<(
            IReadOnlyList<Account> Accounts,
            int Total)> GetAllAsync(
                AccountQueryDto query,
                CancellationToken cancellationToken)
        {
            var accounts = _context.Accounts
                .AsNoTracking()
                .Include(account => account.Company)
                .Include(account => account.ParentAccount)
                .Where(account =>
                    account.CompanyId == query.CompanyId);

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();

                accounts = accounts.Where(account =>
                    account.Code.Contains(search) ||
                    account.Name.Contains(search));
            }

            if (query.Type.HasValue)
            {
                accounts = accounts.Where(account =>
                    account.Type == query.Type.Value);
            }

            if (query.ParentAccountId.HasValue)
            {
                accounts = accounts.Where(account =>
                    account.ParentAccountId ==
                    query.ParentAccountId.Value);
            }

            if (query.IsPostingAccount.HasValue)
            {
                accounts = accounts.Where(account =>
                    account.IsPostingAccount ==
                    query.IsPostingAccount.Value);
            }

            if (query.IsActive.HasValue)
            {
                accounts = accounts.Where(account =>
                    account.IsActive ==
                    query.IsActive.Value);
            }

            var total = await accounts.CountAsync(
                cancellationToken);

            var result = await accounts
                .OrderBy(account => account.Code)
                .ThenBy(account => account.Id)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);

            return (result, total);
        }

        public Task<Account?> GetByIdAsync(
            int id,
            int companyId,
            CancellationToken cancellationToken)
        {
            return _context.Accounts
                .Include(account => account.Company)
                .Include(account => account.ParentAccount)
                .FirstOrDefaultAsync(
                    account =>
                        account.Id == id &&
                        account.CompanyId == companyId,
                    cancellationToken);
        }

        public Task<bool> CompanyExistsAsync(
            int companyId,
            CancellationToken cancellationToken)
        {
            return _context.Companies
                .AsNoTracking()
                .AnyAsync(
                    company => company.Id == companyId,
                    cancellationToken);
        }

        public Task<bool> CodeExistsAsync(
            int companyId,
            string code,
            int? excludeAccountId,
            CancellationToken cancellationToken)
        {
            return _context.Accounts
                .AsNoTracking()
                .AnyAsync(
                    account =>
                        account.CompanyId == companyId &&
                        account.Code == code &&
                        (!excludeAccountId.HasValue ||
                         account.Id != excludeAccountId.Value),
                    cancellationToken);
        }

        public Task<bool> HasChildrenAsync(
            int accountId,
            int companyId,
            CancellationToken cancellationToken)
        {
            return _context.Accounts
                .AsNoTracking()
                .AnyAsync(
                    account =>
                        account.CompanyId == companyId &&
                        account.ParentAccountId == accountId,
                    cancellationToken);
        }

        public async Task<bool> WouldCreateCycleAsync(
            int accountId,
            int parentAccountId,
            int companyId,
            CancellationToken cancellationToken)
        {
            int? currentAccountId = parentAccountId;
            var visitedAccountIds = new HashSet<int>();

            while (currentAccountId.HasValue)
            {
                if (currentAccountId.Value == accountId)
                {
                    return true;
                }

                if (!visitedAccountIds.Add(
                    currentAccountId.Value))
                {
                    return true;
                }

                currentAccountId = await _context.Accounts
                    .AsNoTracking()
                    .Where(account =>
                        account.Id == currentAccountId.Value &&
                        account.CompanyId == companyId)
                    .Select(account =>
                        account.ParentAccountId)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            return false;
        }

        public Task<List<AccountingCompanyLookup>> GetCompaniesAsync(
            CancellationToken cancellationToken)
        {
            return _context.Companies
                .AsNoTracking()
                .OrderBy(company => company.Name)
                .ToListAsync(cancellationToken);
        }

        public Task AddAsync(
            Account account,
            CancellationToken cancellationToken)
        {
            return _context.Accounts
                .AddAsync(
                    account,
                    cancellationToken)
                .AsTask();
        }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            return _context.SaveChangesAsync(
                cancellationToken);
        }

        public async Task<IReadOnlyList<Account>> GetTreeAsync(
            int companyId,
            CancellationToken cancellationToken)
        {
            return await _context.Accounts
                .AsNoTracking()
                .Include(account => account.Company)
                .Include(account => account.ParentAccount)
                .Where(account =>
                    account.CompanyId == companyId)
                .OrderBy(account => account.Code)
                .ToListAsync(cancellationToken);
        }
    }
}
