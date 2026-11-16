using System.Data;
using ExpenseService.API.Data;
using ExpenseService.API.DTOs;
using ExpenseService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseService.API.Repositories
{
    public class CompanySetupRepository
    {
        private readonly CompanySetupDbContext _context;

        public CompanySetupRepository(CompanySetupDbContext context)
        {
            _context = context;
        }

        public Task<List<CompanySetupResponse>> GetAllAsync(
            CancellationToken cancellationToken)
        {
            return (
                from branch in _context.Branches.AsNoTracking()
                join company in _context.Companies.AsNoTracking()
                    on branch.AccountingCompanyId equals (int?)company.Id
                where branch.IsActive
                orderby company.Name, branch.Name
                select new CompanySetupResponse
                {
                    CompanyId = company.Id,
                    CompanyName = company.Name,
                    BranchId = branch.Id,
                    BranchName = branch.Name,
                    Country = company.Country,
                    Currency = company.Currency,
                    InvoiceNumberPrefix = branch.InvoiceNumberPrefix
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<CompanySetupResponse> CreateAsync(
            CompanySetupRequest request,
            string currency,
            CancellationToken cancellationToken)
        {
            await using var transaction = await _context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            if (await _context.Companies.AnyAsync(
                company => company.Name == request.CompanyName,
                cancellationToken))
            {
                throw new InvalidOperationException("A company with this name already exists.");
            }

            if (await _context.Branches.AnyAsync(
                branch => branch.Name == request.BranchName,
                cancellationToken))
            {
                throw new InvalidOperationException("A branch with this name already exists.");
            }

            var company = new Company
            {
                Name = request.CompanyName,
                Country = request.Country,
                Currency = currency
            };

            _context.Companies.Add(company);
            await _context.SaveChangesAsync(cancellationToken);

            var branch = new CompanySetupBranch
            {
                AccountingCompanyId = company.Id,
                Name = request.BranchName,
                CompanyName = company.Name,
                CountryCode = company.Country,
                InvoiceNumberPrefix = request.InvoiceNumberPrefix,
                IsActive = true
            };

            _context.Branches.Add(branch);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new CompanySetupResponse
            {
                CompanyId = company.Id,
                CompanyName = company.Name,
                BranchId = branch.Id,
                BranchName = branch.Name,
                Country = company.Country,
                Currency = company.Currency,
                InvoiceNumberPrefix = branch.InvoiceNumberPrefix
            };
        }
    }
}
