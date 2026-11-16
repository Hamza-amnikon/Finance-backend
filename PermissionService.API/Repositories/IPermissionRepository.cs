using PermissionService.API.Models;

namespace PermissionService.API.Repositories
{
    public interface IPermissionRepository
    {
        Task<List<Permission>> GetAllAsync(
            CancellationToken cancellationToken);

        Task<Permission?> GetByIdAsync(
            int permissionId,
            CancellationToken cancellationToken);

        Task<Permission?> GetByCodeAsync(
            string permissionCode,
            CancellationToken cancellationToken);

        Task AddAsync(
            Permission permission,
            CancellationToken cancellationToken);

        Task UpdateAsync(
            Permission permission,
            byte[] rowVersion,
            CancellationToken cancellationToken);
    }
}