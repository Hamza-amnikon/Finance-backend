using RoleService.API.Models;

namespace RoleService.API.Repositories
{
    public interface IUserRepository
    {
        Task<List<User>> GetAllAsync(
            CancellationToken cancellationToken);

        Task<User?> GetByIdAsync(
            Guid userId,
            CancellationToken cancellationToken);

        Task<User?> GetByMicrosoftIdentityAsync(
            Guid tenantId,
            Guid objectId,
            CancellationToken cancellationToken);

        void Add(User user);

        void SetOriginalRowVersion(
            User user,
            byte[] rowVersion);

        Task SaveChangesAsync(
            CancellationToken cancellationToken);
    }
}
