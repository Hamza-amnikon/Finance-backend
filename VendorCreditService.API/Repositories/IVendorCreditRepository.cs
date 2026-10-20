using VendorCreditService.API.DTOs;
using VendorCreditService.API.Models;

namespace VendorCreditService.API.Repositories
{
    public interface IVendorCreditRepository
    {
        Task<(
            List<VendorCredit> Data,
            int Total)> GetPagedAsync(
                VendorCreditQueryDto query,
                CancellationToken cancellationToken);

        Task<VendorCredit?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken);

        Task<VendorCredit?> GetByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken);

        Task<VendorCredit> CreateAsync(
            VendorCredit credit,
            CancellationToken cancellationToken);

        Task<VendorCredit?> UpdateDraftAsync(
            VendorCredit credit,
            byte[] rowVersion,
            CancellationToken cancellationToken);

        Task<VendorCredit?> ChangeStatusAsync(
            int id,
            VendorCreditStatus target,
            byte[] rowVersion,
            Func<VendorCredit, CancellationToken, Task> validateIssue,
            CancellationToken cancellationToken);
    }
}