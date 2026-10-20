using VendorCreditService.API.DTOs;
using VendorCreditService.API.Models;

namespace VendorCreditService.API.Repositories
{
    public interface ICreditApplicationRepository
    {
        Task<VendorCreditApplication?> GetByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken);

        Task<VendorCreditApplication> ApplyAsync(
            VendorCreditApplication application,
            byte[] creditRowVersion,
            byte[] billRowVersion,
            CancellationToken cancellationToken);

        Task<VendorCreditApplication?> VoidAsync(
            int creditId,
            int applicationId,
            VoidCreditApplicationDto dto,
            CancellationToken cancellationToken);
    }
}
