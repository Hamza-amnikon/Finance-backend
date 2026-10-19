using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Repositories
{
    public interface IBranchRepository
    {
        Task<BranchMaster?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken);

        Task<BranchMaster> UpdateInvoiceProfileAsync(
            BranchMaster branch,
            byte[] rowVersion,
            CancellationToken cancellationToken);

        Task<List<BranchMaster>> GetActiveAsync(
            CancellationToken cancellationToken);

        Task<BranchMaster?> GetByNameAsync(
            string name,
            CancellationToken cancellationToken);
    }
}