using PermissionService.API.DTOs;

namespace PermissionService.API.Services
{
    public interface IPermissionService
    {
        Task<List<PermissionResponse>> GetAllAsync(
            CancellationToken cancellationToken);

        Task<PermissionResponse?> GetByIdAsync(
            int permissionId,
            CancellationToken cancellationToken);

        Task<PermissionResponse> CreateAsync(
            PermissionRequest request,
            CancellationToken cancellationToken);

        Task<PermissionResponse?> UpdateAsync(
            int permissionId,
            PermissionUpdateRequest request,
            CancellationToken cancellationToken);
    }
}