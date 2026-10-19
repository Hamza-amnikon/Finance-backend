using Microsoft.EntityFrameworkCore;
using SalesInvoiceService.API.Data;
using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Repositories
{
    public class BranchRepository : IBranchRepository
    {
        private readonly SalesInvoiceDbContext _context;

        public BranchRepository(SalesInvoiceDbContext context)
        {
            _context = context;
        }

        public Task<BranchMaster?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            return _context.BranchMasters.FirstOrDefaultAsync(
                branch => branch.Id == id && branch.IsActive,
                cancellationToken);
        }

        public async Task<BranchMaster> UpdateInvoiceProfileAsync(
            BranchMaster branch,
            byte[] rowVersion,
            CancellationToken cancellationToken)
        {
            _context.Entry(branch).Property(entry => entry.RowVersion)
                .OriginalValue = rowVersion;

            await _context.SaveChangesAsync(cancellationToken);

            return branch;
        }

        public async Task<List<BranchMaster>> GetActiveAsync(
            CancellationToken cancellationToken)
        {
            return await _context.BranchMasters
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<BranchMaster?> GetByNameAsync(
            string name,
            CancellationToken cancellationToken)
        {
            var value = name.Trim();

            return await _context.BranchMasters
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Name == value && x.IsActive,
                    cancellationToken);
        }
    }
}