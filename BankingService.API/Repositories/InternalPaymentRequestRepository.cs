using BankingService.API.Data;
using BankingService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BankingService.API.Repositories
{
    public class InternalPaymentRequestRepository(BankingDbContext database)
    {
        public IQueryable<InternalPaymentRequest> Query()
        {
            return database.InternalPaymentRequests.AsNoTracking().Include(row => row.Lines);
        }

        public Task<InternalPaymentRequest?> GetAsync(int id, CancellationToken cancellationToken)
        {
            return database.InternalPaymentRequests.Include(row => row.Lines)
                .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        }

        public void Add(InternalPaymentRequest request)
        {
            database.InternalPaymentRequests.Add(request);
        }

        public void SetVersion(InternalPaymentRequest request, byte[] version)
        {
            database.Entry(request).Property(row => row.RowVersion).OriginalValue = version;
        }

        public Task<int> SaveAsync(CancellationToken cancellationToken)
        {
            return database.SaveChangesAsync(cancellationToken);
        }
    }
}
