using BankingService.API.Data;
using BankingService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BankingService.API.Repositories
{
    public class BankAccountRepository : IBankAccountRepository
    {
        private readonly BankingDbContext _context;

        public BankAccountRepository(
            BankingDbContext context)
        {
            _context = context;
        }

        public IQueryable<BankAccount> Query()
        {
            return _context.BankAccounts.AsNoTracking();
        }

        public async Task<BankAccount?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            return await _context.BankAccounts
                .FirstOrDefaultAsync(
                    account => account.Id == id,
                    cancellationToken);
        }

        public async Task<BankingCompanyLookup?> GetCompanyAsync(
            int companyId,
            CancellationToken cancellationToken)
        {
            return await _context.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    company => company.Id == companyId,
                    cancellationToken);
        }

        public async Task<BankingCurrencyLookup?> GetCurrencyAsync(
            string currencyCode,
            CancellationToken cancellationToken)
        {
            return await _context.Currencies
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    currency => currency.CurrencyCode == currencyCode,
                    cancellationToken);
        }

        public async Task<List<BankingCompanyLookup>> GetCompaniesAsync(
            CancellationToken cancellationToken)
        {
            return await _context.Companies
                .AsNoTracking()
                .OrderBy(company => company.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<BankingCurrencyLookup>> GetCurrenciesAsync(
            CancellationToken cancellationToken)
        {
            return await _context.Currencies
                .AsNoTracking()
                .Where(currency => currency.IsActive)
                .OrderBy(currency => currency.CurrencyCode)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(
            BankAccount account,
            CancellationToken cancellationToken)
        {
            await _context.BankAccounts.AddAsync(
                account,
                cancellationToken);
        }

        public async Task<int> SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            return await _context.SaveChangesAsync(
                cancellationToken);
        }
    }
}