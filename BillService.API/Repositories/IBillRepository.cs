using BillService.API.DTOs;
using BillService.API.Models;

namespace BillService.API.Repositories
{
    public interface IBillRepository
    {
        Task<Bill?> GetByIdAsync(int id, CancellationToken cancellationToken);
        Task<Bill?> GetByRequestIdAsync(Guid requestId, CancellationToken cancellationToken);
        Task<(List<Bill> Data, int Total)> GetPagedAsync(BillQueryDto query, CancellationToken cancellationToken);
        Task<Bill> CreateAsync(Bill bill, CancellationToken cancellationToken);
        Task<Bill?> UpdateDraftAsync(Bill bill, byte[] rowVersion, CancellationToken cancellationToken);
        Task<Bill?> ChangeStatusAsync(int id, BillStatus status, byte[] rowVersion,
            Func<Bill, CancellationToken, Task> validateIssue, CancellationToken cancellationToken);
    }
}
