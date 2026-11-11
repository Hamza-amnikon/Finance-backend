using RoleService.API.Models;

namespace RoleService.API.Repositories
{
    public interface IRoleRepository
    {
        Task<List<Roles>> GetAllAsync(
            CancellationToken cancellationToken);

        Task<List<Roles>> GetActiveAsync(
            CancellationToken cancellationToken);

        Task<Roles?> GetByIdAsync(
            int roleId,
            CancellationToken cancellationToken);

        void Add(Roles role);

        void SetOriginalRowVersion(
            Roles role,
            byte[] rowVersion);

        void Remove(Roles role);

        Task SaveChangesAsync(
            CancellationToken cancellationToken);
    }
}
