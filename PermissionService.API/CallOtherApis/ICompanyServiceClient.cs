using PermissionService.API.DTOs;

namespace PermissionService.API.CallOtherApis
{
    public interface ICompanyServiceClient
    {
        Task<CompanyLookupResponse?> GetByIdAsync(
            int companyId,
            CancellationToken cancellationToken);
    }
}
