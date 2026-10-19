using SalesInvoiceService.API.DTOs;
using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Services
{
    public interface IBranchService
    {
        Task<BranchInvoiceProfileDto?> GetInvoiceProfileAsync(
            int id,
            CancellationToken cancellationToken);

        Task<BranchInvoiceProfileDto?> UpdateInvoiceProfileAsync(
            int id,
            UpdateBranchInvoiceProfileDto dto,
            CancellationToken cancellationToken);

        Task<List<BranchMaster>> GetActiveAsync(
            CancellationToken cancellationToken);

        Task<BranchMaster?> GetByNameAsync(
            string name,
            CancellationToken cancellationToken);
    }
}